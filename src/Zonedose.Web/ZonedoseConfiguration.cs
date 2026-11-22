namespace Zonedose.Web;

/// <summary>A tenant as configured for this environment: its site host and its admin host.</summary>
public sealed record TenantLayout(string Name, string SiteHost, string AdminHost, string RecipeName)
{
    /// <summary>Every host the tenant answers on, as Orchard's RequestUrlHost (comma separated).</summary>
    public string RequestUrlHost =>
        string.Equals(SiteHost, AdminHost, StringComparison.OrdinalIgnoreCase) ? SiteHost : $"{SiteHost},{AdminHost}";
}

/// <summary>
/// Maps the ZONEDOSE_* environment variables onto Orchard's configuration, so
/// appsettings.json holds the tenant layout and no secret.
///
/// <list type="bullet">
/// <item>ZONEDOSE_DB_CONNECTION: the application role (data only), for normal running.</item>
/// <item>ZONEDOSE_DB_MIGRATION_CONNECTION: the migrator role (owns the tables), used
/// only by the migrate step: setup, migrations, hostnames (decision 0002).</item>
/// <item>ZONEDOSE_SETUP_ADMIN_PASSWORD: password of the local super user that
/// Orchard creates in each tenant at setup.</item>
/// <item>ZONEDOSE_HOST_&lt;TENANT&gt; and ZONEDOSE_ADMIN_HOST_&lt;TENANT&gt;: the site
/// and admin hostnames of a tenant in this environment (e.g. ZONEDOSE_HOST_ZONEDOSE).</item>
/// <item>ZONEDOSE_GOOGLE_CLIENT_ID, ZONEDOSE_GOOGLE_CLIENT_SECRET: the Google OAuth
/// client of this environment (docs/google-oauth.md).</item>
/// <item>ZONEDOSE_ADMIN_EMAIL: the account created with this verified email is an
/// active administrator (only at creation).</item>
/// </list>
///
/// Orchard never stores the connection string in App_Data when it comes from
/// configuration, so the role used is always the one given here.
/// </summary>
public static class ZonedoseConfiguration
{
    public const string AdminHostKey = "Zonedose:AdminHost";
    private const string TenantsSection = "OrchardCore:OrchardCore_AutoSetup:Tenants";

    public static IReadOnlyList<TenantLayout> Apply(ConfigurationManager configuration, bool migrate)
    {
        var variable = migrate ? "ZONEDOSE_DB_MIGRATION_CONNECTION" : "ZONEDOSE_DB_CONNECTION";
        var connection = configuration[variable];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException($"{variable} is not set: see .env.example.");
        }
        // Only AutoSetup reads it, and only the migrate step can complete a setup
        var setupPassword = configuration["ZONEDOSE_SETUP_ADMIN_PASSWORD"];

        var tenants = new List<TenantLayout>();
        var values = new Dictionary<string, string?>();
        foreach (var tenant in configuration.GetSection(TenantsSection).GetChildren())
        {
            var name = tenant["ShellName"];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var key = name.ToUpperInvariant();
            var siteHost = configuration[$"ZONEDOSE_HOST_{key}"] ?? tenant["RequestUrlHost"];
            var adminHost = configuration[$"ZONEDOSE_ADMIN_HOST_{key}"] ?? tenant["AdminHost"];
            if (string.IsNullOrEmpty(siteHost) || string.IsNullOrEmpty(adminHost))
            {
                throw new InvalidOperationException($"Tenant '{name}' needs a site host and an admin host.");
            }
            var recipe = tenant["RecipeName"] ?? throw new InvalidOperationException($"Tenant '{name}' needs a RecipeName.");
            var layout = new TenantLayout(name, siteHost, adminHost, recipe);
            tenants.Add(layout);

            values[$"{tenant.Path}:DatabaseConnectionString"] = connection;
            values[$"{tenant.Path}:RequestUrlHost"] = layout.RequestUrlHost;
            values[$"OrchardCore:{name}:ConnectionString"] = connection;
            // Read by the platform module of each tenant (tenant configuration)
            values[$"OrchardCore:{name}:{AdminHostKey}"] = adminHost;
            if (!string.IsNullOrEmpty(setupPassword))
            {
                values[$"{tenant.Path}:AdminPassword"] = setupPassword;
            }
        }
        // Shared by every tenant (tenant configuration section Zonedose:Accounts)
        values["OrchardCore:Zonedose:Accounts:GoogleClientId"] = configuration["ZONEDOSE_GOOGLE_CLIENT_ID"];
        values["OrchardCore:Zonedose:Accounts:GoogleClientSecret"] = configuration["ZONEDOSE_GOOGLE_CLIENT_SECRET"];
        values["OrchardCore:Zonedose:Accounts:AdminEmail"] = configuration["ZONEDOSE_ADMIN_EMAIL"];

        configuration.AddInMemoryCollection(values);
        return tenants;
    }
}
