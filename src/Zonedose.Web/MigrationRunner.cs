using System.Net;
using System.Text.Json.Nodes;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace Zonedose.Web;

/// <summary>Outcome of the migrate step for one tenant.</summary>
public sealed record TenantMigration(
    string Tenant,
    bool Running,
    bool HostsUpdated,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyList<string> MissingFeatures,
    IReadOnlyList<string> PendingFeatures)
{
    public bool Ok => Running && MissingFeatures.Count == 0 && PendingFeatures.Count == 0;
}

/// <summary>
/// The migrate step (decision 0002): run with the migrator role before the
/// application serves traffic with the app role.
///
/// <list type="number">
/// <item>Hostnames: Orchard keeps a tenant's hosts in App_Data (tenants.json),
/// which wins over configuration. Existing tenants get the hosts configured for
/// this environment written back, so hostnames are configuration too.</item>
/// <item>Setup: new tenants are set up by AutoSetup on their first request, the
/// Default tenant first (it also creates the others), as in production.</item>
/// <item>Features: a setup recipe runs only when its tenant is created. Every
/// feature its <c>feature</c> steps enable is enabled on existing tenants too, so
/// a feature added to a recipe reaches every environment. Features are never
/// disabled here: that stays an explicit decision.</item>
/// <item>Migrations: activating a tenant runs its pending migrations; they are
/// run once more explicitly, then nothing may be left pending.</item>
/// </list>
/// </summary>
public static class MigrationRunner
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(5);

    /// <param name="services">The application's root services.</param>
    /// <param name="createClient">An HTTP client to the running application.</param>
    /// <param name="tenants">The tenants configured for this environment, Default first.</param>
    public static async Task<IReadOnlyList<TenantMigration>> RunAsync(
        IServiceProvider services,
        Func<HttpClient> createClient,
        IReadOnlyList<TenantLayout> tenants,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var shellHost = services.GetRequiredService<IShellHost>();
        await shellHost.InitializeAsync();

        var hostsUpdated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tenant in tenants)
        {
            if (shellHost.TryGetSettings(tenant.Name, out var settings)
                && !settings.IsUninitialized()
                && !SameHosts(settings.RequestUrlHost, tenant.RequestUrlHost))
            {
                logger.LogInformation("Tenant {Tenant}: hosts {Old} -> {New}", tenant.Name, settings.RequestUrlHost, tenant.RequestUrlHost);
                var updated = new ShellSettings(settings) { RequestUrlHost = tenant.RequestUrlHost };
                await shellHost.UpdateShellSettingsAsync(updated);
                hostsUpdated.Add(tenant.Name);
            }
        }

        using var client = createClient();
        foreach (var tenant in tenants)
        {
            await WarmUpAsync(client, tenant, logger, cancellationToken);
        }

        var application = services.GetRequiredService<IApplicationContext>().Application;
        var results = new List<TenantMigration>();
        foreach (var tenant in tenants)
        {
            if (!shellHost.TryGetSettings(tenant.Name, out var settings) || !settings.IsRunning())
            {
                results.Add(new TenantMigration(tenant.Name, false, hostsUpdated.Contains(tenant.Name), [], [], []));
                continue;
            }

            var wanted = RecipeFeatures(application, tenant.RecipeName);
            IReadOnlyList<string> enabled = [];
            IReadOnlyList<string> missing = [];
            var featureScope = await shellHost.GetScopeAsync(settings);
            await featureScope.UsingAsync(async shellScope =>
            {
                var features = shellScope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
                var active = (await features.GetEnabledFeaturesAsync()).Select(f => f.Id).ToHashSet();
                var toEnable = (await features.GetAvailableFeaturesAsync())
                    .Where(f => wanted.Contains(f.Id) && !active.Contains(f.Id)).ToArray();
                if (toEnable.Length > 0)
                {
                    logger.LogInformation("Tenant {Tenant}: enabling {Features}", tenant.Name, string.Join(", ", toEnable.Select(f => f.Id)));
                    await features.EnableFeaturesAsync(toEnable, force: true);
                }
                enabled = [.. toEnable.Select(f => f.Id)];
                var known = (await features.GetAvailableFeaturesAsync()).Select(f => f.Id).ToHashSet();
                missing = [.. wanted.Where(id => !known.Contains(id)).Order()];
            });

            // A new scope: enabling features rebuilt the tenant, and its migrations run now
            IReadOnlyList<string> pending = [];
            settings = shellHost.GetSettings(tenant.Name);
            var scope = await shellHost.GetScopeAsync(settings);
            await scope.UsingAsync(async shellScope =>
            {
                var migrations = shellScope.ServiceProvider.GetRequiredService<IDataMigrationManager>();
                await migrations.UpdateAllFeaturesAsync();
                pending = [.. await migrations.GetFeaturesThatNeedUpdateAsync()];
            });
            results.Add(new TenantMigration(tenant.Name, true, hostsUpdated.Contains(tenant.Name), enabled, missing, pending));
        }
        return results;
    }

    /// <summary>
    /// The features a setup recipe enables, read from the recipe files the modules
    /// ship (Recipes/*.recipe.json), independently of the tenant's own features.
    /// </summary>
    public static IReadOnlySet<string> RecipeFeatures(Application application, string recipeName)
    {
        foreach (var module in application.Modules)
        {
            foreach (var path in module.AssetPaths.Where(p => p.EndsWith(".recipe.json", StringComparison.OrdinalIgnoreCase)))
            {
                var relative = path[(path.IndexOf("/Recipes/", StringComparison.OrdinalIgnoreCase) + 1)..];
                var file = module.GetFileInfo(relative);
                if (!file.Exists)
                {
                    continue;
                }
                using var stream = file.CreateReadStream();
                // Orchard's own recipes contain comments
                var recipe = JsonNode.Parse(stream, documentOptions: new System.Text.Json.JsonDocumentOptions
                {
                    CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                if (!string.Equals(recipe?["name"]?.GetValue<string>(), recipeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return recipe!["steps"]!.AsArray()
                    .Where(step => step?["name"]?.GetValue<string>() == "feature")
                    .SelectMany(step => step!["enable"]?.AsArray() ?? [])
                    .Select(id => id!.GetValue<string>())
                    .ToHashSet(StringComparer.Ordinal);
            }
        }
        throw new InvalidOperationException($"Setup recipe '{recipeName}' not found in any module.");
    }

    private static bool SameHosts(string current, string wanted)
    {
        static string[] Split(string hosts) =>
            [.. hosts.Split(ShellSettings.HostSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.ToLowerInvariant()).Order()];
        return Split(current).SequenceEqual(Split(wanted));
    }

    /// <summary>
    /// Requests the tenant until it answers without 503: the first request to a
    /// new tenant runs its setup, and a tenant being set up answers 503.
    /// </summary>
    private static async Task WarmUpAsync(HttpClient client, TenantLayout tenant, ILogger logger, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + SetupTimeout;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
            request.Headers.Host = tenant.SiteHost;
            using var response = await client.SendAsync(request, cancellationToken);
            // Orchard marks "tenant initializing" with Retry-After; a 503 from the
            // health endpoint itself (pending migrations) is checked afterwards
            var initializing = response.StatusCode == HttpStatusCode.ServiceUnavailable
                && response.Headers.RetryAfter is not null;
            if (!initializing || DateTime.UtcNow > deadline)
            {
                logger.LogInformation("Tenant {Tenant}: {Status}", tenant.Name, (int)response.StatusCode);
                return;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
