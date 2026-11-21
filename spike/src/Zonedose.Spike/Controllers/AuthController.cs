using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace Zonedose.Spike.Controllers;

/// <summary>
/// The same contract the Angular session service already uses with FastAPI
/// (/api/auth/me, /refresh, /logout, /google/login), on top of Orchard's
/// ASP.NET Identity cookie.
/// </summary>
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IUser> userManager,
    SignInManager<IUser> signInManager,
    IAuthenticationSchemeProvider schemes,
    IUserService userService,
    IHostEnvironment environment) : ApiControllerBase(userManager)
{
    [HttpGet("google/login")]
    public async Task<IActionResult> GoogleLogin()
    {
        if (await schemes.GetSchemeAsync("Google") is null)
        {
            return Redirect("/login?error=unavailable");
        }
        // Orchard's own callback finds or auto-registers the user, runs the
        // registration events (pending / ADMIN_EMAIL) and signs in
        var callback = Url.Action("ExternalLoginCallback", "ExternalAuthentications",
            new { area = "OrchardCore.Users", returnUrl = "/" });
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Google", callback);
        return Challenge(properties, "Google");
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    /// <summary>
    /// The Identity cookie renews itself (sliding expiration) on any request, so
    /// there is nothing to rotate: 204 while the session is alive, 401 after.
    /// The Angular refresh interceptor keeps working unchanged.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh() =>
        await CurrentUserAsync() is null ? Unauthorized() : NoContent();

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>
    /// Development only: registers a user through the same UserService.RegisterAsync
    /// call Orchard's Google auto-registration makes, without a Google round trip.
    /// </summary>
    [HttpPost("dev/register")]
    public async Task<IActionResult> DevRegister([FromBody] DevRegisterRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }
        // Returning user: Orchard's callback finds them by login and signs them in
        if (await userManager.FindByEmailAsync(request.Email) is User existing)
        {
            if (!existing.IsEnabled)
            {
                return Forbid();
            }
            await signInManager.SignInAsync(existing, isPersistent: false);
            return Ok(ToDto(existing));
        }

        var errors = new List<string>();
        var user = await userService.RegisterAsync(new RegisterUserForm
        {
            UserName = request.Email.Split('@')[0],
            Email = request.Email,
            Password = null,
        }, (_, message) => errors.Add(message));
        if (user is null)
        {
            return BadRequest(new { errors });
        }
        await userManager.AddLoginAsync(user, new UserLoginInfo("Google", "dev-" + request.Email, "Google"));
        return Ok(ToDto((User)user));
    }

    public sealed record DevRegisterRequest(string Email);
}
