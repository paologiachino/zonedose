using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Zonedose.Spike.Services;

namespace Zonedose.Spike.Controllers;

/// <summary>
/// JSON API under /api/. Orchard's antiforgery tokens are for its Razor forms;
/// the API uses SameSite cookies plus the X-Requested-With header (CsrfMiddleware).
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
public abstract class ApiControllerBase(UserManager<IUser> userManager) : ControllerBase
{
    protected UserManager<IUser> Users => userManager;

    protected async Task<User?> CurrentUserAsync() =>
        User.Identity?.IsAuthenticated == true ? await userManager.GetUserAsync(User) as User : null;

    /// <summary>The signed-in active user, or the error to return (401 / 403).</summary>
    protected async Task<(User? User, IActionResult? Error)> RequireActiveAsync(bool admin = false)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return (null, Unauthorized());
        }
        if (AccountStatus.Of(user) != AccountStatus.Active || (admin && AccountStatus.RoleOf(user) != "admin"))
        {
            return (null, Forbid());
        }
        return (user, null);
    }

    protected static object ToDto(User user) => new
    {
        id = user.UserId,
        email = user.Email,
        display_name = (string?)null,
        status = AccountStatus.Of(user),
        role = AccountStatus.RoleOf(user),
    };
}
