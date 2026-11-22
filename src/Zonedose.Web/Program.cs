using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using OrchardCore.Environment.Shell;
using Zonedose.Web;

// "migrate": the one-off step run with the migrator role before each deploy
// (setup, migrations, hostnames), then exit. Otherwise: serve with the app role.
var migrate = args.Length > 0 && args[0] == "migrate";

var builder = WebApplication.CreateBuilder(args);

// Secrets and hostnames come from ZONEDOSE_* environment variables
var tenants = ZonedoseConfiguration.Apply(builder.Configuration, migrate);
builder.Services.AddSingleton(tenants);

// AutoSetup must be available before a tenant exists: every tenant is set up
// from configuration and recipes, never through the setup screens
builder.Services.AddOrchardCms().AddSetupFeatures("OrchardCore.AutoSetup");

if (migrate)
{
    // Only reachable from inside the process: the step talks to itself
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

var app = builder.Build();

// A host that matches no tenant gets 404. Orchard ends such requests without
// setting a status, which would otherwise go out as an empty 200.
app.Use(async (context, next) =>
{
    await next();
    if (!context.Response.HasStarted && context.Features.Get<ShellContextFeature>() is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }
});

app.UseStaticFiles();
app.UseOrchardCore();

if (!migrate)
{
    await app.RunAsync();
    return 0;
}

await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()!.Addresses.First();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Zonedose.Migrate");

var results = await MigrationRunner.RunAsync(
    app.Services, () => new HttpClient { BaseAddress = new Uri(address) }, tenants, logger);

foreach (var result in results)
{
    logger.LogInformation(
        "Tenant {Tenant}: running={Running}, hosts updated={Hosts}, pending migrations=[{Pending}]",
        result.Tenant, result.Running, result.HostsUpdated, string.Join(", ", result.PendingFeatures));
}
await app.StopAsync();

var ok = results.All(r => r.Ok);
Console.WriteLine(ok ? "Migrate step succeeded." : "Migrate step FAILED: see the log above.");
return ok ? 0 : 1;

/// <summary>Entry point, public for the integration tests (WebApplicationFactory).</summary>
public partial class Program;
