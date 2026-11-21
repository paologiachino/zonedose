using OrchardCore.Environment.Shell;
using Zonedose.Web;

var builder = WebApplication.CreateBuilder(args);

// Secrets and hostnames come from ZONEDOSE_* environment variables
ZonedoseConfiguration.Apply(builder.Configuration);

// AutoSetup must be available before a tenant exists: every tenant is set up
// from configuration and recipes, never through the setup screens
builder.Services.AddOrchardCms().AddSetupFeatures("OrchardCore.AutoSetup");

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

await app.RunAsync();

/// <summary>Entry point, public for the integration tests (WebApplicationFactory).</summary>
public partial class Program;
