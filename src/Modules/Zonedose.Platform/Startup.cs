using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace Zonedose.Platform;

public sealed class Startup : StartupBase
{
    /// <summary>Tenant configuration key with the admin hostname (set by the host from ZONEDOSE_ADMIN_HOST_*).</summary>
    public const string AdminHostKey = "Zonedose:AdminHost";

    /// <summary>
    /// After the reverse proxy (-300, which sets the real host) and before
    /// authentication (-150): a blocked admin request never reaches the sign-in logic.
    /// </summary>
    public override int ConfigureOrder => OrchardCoreConstants.ConfigureOrder.ReverseProxy + 100;

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The admin answers only on the tenant's admin hostname, so the public
        // site host never exposes it. With no admin host configured the admin
        // is closed everywhere (fail closed).
        var adminHost = serviceProvider.GetRequiredService<IShellConfiguration>()[AdminHostKey];
        var adminPrefix = "/" + serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(adminPrefix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(context.Request.Host.Host, adminHost, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next();
        });

        // Readiness of a tenant: running and with no migration pending. A
        // pending migration means the migrate step has not run for this
        // version: the deploy must stop (decision 0002).
        routes.MapGet("/api/health", async (ShellSettings shell, IDataMigrationManager migrations) =>
        {
            var pending = (await migrations.GetFeaturesThatNeedUpdateAsync()).ToArray();
            return pending.Length == 0
                ? Results.Ok(new { status = "ok", tenant = shell.Name })
                : Results.Json(new { status = "migrations-pending", tenant = shell.Name }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }
}
