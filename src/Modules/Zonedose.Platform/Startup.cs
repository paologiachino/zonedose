using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace Zonedose.Platform;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Liveness of a tenant: answers once the tenant is set up and running.
        // Says nothing about the database beyond the tenant having started.
        routes.MapGet("/api/health", (ShellSettings shell) =>
            Results.Ok(new { status = "ok", tenant = shell.Name }));
    }
}
