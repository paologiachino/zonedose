using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using Zonedose.Accounts.Services;

namespace Zonedose.Accounts.Controllers;

/// <summary>
/// Google sign-in and the session, with the contract the Angular frontend
/// already uses (Sportlab's /api/auth):
///
/// <list type="bullet">
/// <item>GET  /api/auth/google/login: redirect to Google (state and PKCE by ASP.NET).</item>
/// <item>GET  /api/auth/google/callback: handled by the Google handler, which then
/// sends the browser to /api/auth/google/finish.</item>
/// <item>GET  /api/auth/me, POST /api/auth/refresh, POST /api/auth/logout.</item>
/// </list>
/// Every failure of the sign-in ends on /login?error=&lt;code&gt;: cancelled, failed,
/// unverified_email, unavailable, disabled.
/// </summary>
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IUser> userManager,
    SignInManager<IUser> signInManager,
    IAuthenticationSchemeProvider schemes,
    GoogleSignIn googleSignIn,
    ILogger<AuthController> logger) : ApiControllerBase(userManager)
{
    public const string FinishPath = "/api/auth/google/finish";

    [HttpGet("google/login")]
    public async Task<IActionResult> GoogleLogin()
    {
        if (await schemes.GetSchemeAsync(Account.GoogleProvider) is null)
        {
            logger.LogWarning("Google sign-in requested but not configured");
            return LoginFailed(LoginError.Unavailable);
        }
        var properties = signInManager.ConfigureExternalAuthenticationProperties(Account.GoogleProvider, FinishPath);
        return Challenge(properties, Account.GoogleProvider);
    }

    /// <summary>End of the Google redirect: only browsers coming back from Google call it.</summary>
    [HttpGet("google/finish")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleFinish()
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info is null || info.LoginProvider != Account.GoogleProvider)
        {
            return LoginFailed(LoginError.Failed);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(info.ProviderKey) || string.IsNullOrEmpty(email))
        {
            return LoginFailed(LoginError.Failed);
        }
        if (info.Principal.FindFirstValue(GoogleClaims.EmailVerified) != "true")
        {
            // Without a verified address, ADMIN_EMAIL could be claimed by anyone
            return LoginFailed(LoginError.UnverifiedEmail);
        }

        var (user, outcome) = await googleSignIn.SignInAsync(
            new GoogleIdentity(info.ProviderKey, email, info.Principal.FindFirstValue(ClaimTypes.Name)));
        if (user is null)
        {
            return LoginFailed(outcome == SignInOutcome.Disabled ? LoginError.Disabled : LoginError.Failed);
        }
        await signInManager.SignInAsync(user, isPersistent: true);
        return Redirect("/");
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUserAsync();
        return user is null ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Not signed in") : Ok(ToUser(user));
    }

    /// <summary>
    /// The session renews itself (sliding) on every request, so there is nothing
    /// to rotate: 204 while it is alive, 401 (and the cookie cleared) after.
    /// The Angular refresh interceptor works unchanged.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (await CurrentUserAsync() is not null)
        {
            return NoContent();
        }
        await signInManager.SignOutAsync();
        return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Session ended");
    }

    /// <summary>Ends the session on the server and clears the cookie. Safe when already signed out.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    private static RedirectResult LoginFailed(string error) => new($"/login?error={error}");
}

/// <summary>Codes the login page turns into a message.</summary>
public static class LoginError
{
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public const string UnverifiedEmail = "unverified_email";
    public const string Unavailable = "unavailable";
    public const string Disabled = "disabled";
}

public static class GoogleClaims
{
    public const string EmailVerified = "email_verified";
}
