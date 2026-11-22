using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using Zonedose.Accounts.Controllers;
using Zonedose.Accounts.Services;
using Zonedose.Accounts.Sessions;

namespace Zonedose.Accounts;

public sealed class Startup(IShellConfiguration configuration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var settings = configuration.GetSection(AccountSettings.Section).Get<AccountSettings>() ?? new AccountSettings();
        services.Configure<AccountSettings>(configuration.GetSection(AccountSettings.Section));
        // The host may provide its own clock (tests do)
        services.TryAddSingleton(TimeProvider.System);

        services.AddDataMigration<SessionMigrations>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, ConfigureSessionCookie>();
        // Orchard's admin reads roles from the cookie: re-check them often
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddScoped<GoogleSignIn>();
        services.AddScoped<UserAdministration>();

        if (settings.GoogleConfigured)
        {
            services.AddAuthentication().AddGoogle(Account.GoogleProvider, options => ConfigureGoogle(options, settings));
        }
    }

    private static void ConfigureGoogle(GoogleOptions options, AccountSettings settings)
    {
        options.ClientId = settings.GoogleClientId!;
        options.ClientSecret = settings.GoogleClientSecret!;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        // The redirect URI registered in Google Cloud, as in Sportlab
        options.CallbackPath = "/api/auth/google/callback";
        options.UsePkce = true;
        options.SaveTokens = false;

        // Google reports a verified address as email_verified (userinfo v3) or
        // verified_email (v2): either way it becomes the claim checked at sign-in
        options.ClaimActions.MapCustomJson(GoogleClaims.EmailVerified, json =>
            (json.TryGetProperty("email_verified", out var value) || json.TryGetProperty("verified_email", out value))
            && (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"))
                ? "true" : "false");

        options.Events.OnAccessDenied = context =>
        {
            // The person pressed "Cancel" on Google's consent screen
            context.Response.Redirect($"/login?error={LoginError.Cancelled}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            var error = context.Failure is HttpRequestException ? LoginError.Unavailable : LoginError.Failed;
            context.Response.Redirect($"/login?error={error}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }
}
