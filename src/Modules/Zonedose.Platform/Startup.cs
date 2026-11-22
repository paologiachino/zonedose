using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace Zonedose.Platform;

public sealed class Startup : StartupBase
{
    /// <summary>Tenant configuration key with the admin hostname (set by the host from ZONEDOSE_ADMIN_HOST_*).</summary>
    public const string AdminHostKey = "Zonedose:AdminHost";

    /// <summary>Header required on state-changing API requests (the Angular CSRF interceptor adds it).</summary>
    public const string CsrfHeader = "X-Requested-With";

    /// <summary>
    /// After the reverse proxy (-300, which sets the real host) and before
    /// authentication (-150): a blocked request never reaches the sign-in logic.
    /// </summary>
    public override int ConfigureOrder => OrchardCoreConstants.ConfigureOrder.ReverseProxy + 100;

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The public site host serves the API only: Orchard's own pages (admin,
        // local sign-in, account pages) answer only on the tenant's admin
        // hostname, which is reachable from the internal network only. With no
        // admin host configured, only the API answers anywhere (fail closed).
        var adminHost = serviceProvider.GetRequiredService<IShellConfiguration>()[AdminHostKey];
        app.Use(async (context, next) =>
        {
            if (!IsApi(context.Request)
                && !string.Equals(context.Request.Host.Host, adminHost, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next();
        });

        // CSRF, second barrier after the SameSite=Strict session cookie: a
        // state-changing API request must carry X-Requested-With, which a form
        // or a link on another site cannot add (setting it from another origin
        // would need CORS, which is off). New endpoints are covered by default.
        app.Use(async (context, next) =>
        {
            if (IsApi(context.Request)
                && !HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method)
                && !HttpMethods.IsOptions(context.Request.Method)
                && string.IsNullOrEmpty(context.Request.Headers[CsrfHeader]))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { detail = $"Missing {CsrfHeader} header" });
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

    private static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");
}
