using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Users.Events;
using Zonedose.Spike.Migrations;
using Zonedose.Spike.Services;

namespace Zonedose.Spike;

public sealed class Startup : StartupBase
{
    private static readonly HashSet<string> UnsafeMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SpikeMigrations>();
        services.AddScoped<IRegistrationFormEvents, ApprovalRegistrationEvents>();
        services.AddSingleton<ActivityStore>();

        // API callers get 401/403, not a redirect to Orchard's login page
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            var toLogin = options.Events.OnRedirectToLogin;
            var toDenied = options.Events.OnRedirectToAccessDenied;
            options.Events.OnRedirectToLogin = ctx => IsApi(ctx.Request)
                ? Status(ctx.Response, StatusCodes.Status401Unauthorized) : toLogin(ctx);
            options.Events.OnRedirectToAccessDenied = ctx => IsApi(ctx.Request)
                ? Status(ctx.Response, StatusCodes.Status403Forbidden) : toDenied(ctx);
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // CSRF second barrier, as in sportlab's backend/app/core/csrf.py
        app.Use(async (context, next) =>
        {
            if (UnsafeMethods.Contains(context.Request.Method)
                && IsApi(context.Request)
                && string.IsNullOrEmpty(context.Request.Headers["X-Requested-With"]))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { detail = "Missing X-Requested-With header" });
                return;
            }
            await next();
        });
    }

    private static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");

    private static Task Status(HttpResponse response, int code)
    {
        response.StatusCode = code;
        return Task.CompletedTask;
    }
}
