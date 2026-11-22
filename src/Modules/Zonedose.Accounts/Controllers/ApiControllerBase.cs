using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Zonedose.Accounts.Services;

namespace Zonedose.Accounts.Controllers;

/// <summary>
/// JSON API under /api. CSRF: the session cookie is SameSite=Strict, and the
/// platform module requires X-Requested-With on state-changing requests, so
/// Orchard's antiforgery tokens (meant for its Razor forms) are not used here.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
public abstract class ApiControllerBase(UserManager<IUser> userManager) : ControllerBase
{
    /// <summary>The signed-in user, read from the database on every request.</summary>
    protected async Task<User?> CurrentUserAsync() =>
        User.Identity?.IsAuthenticated == true ? await userManager.GetUserAsync(User) as User : null;

    /// <summary>The signed-in active user (and administrator when asked), or the error to return.</summary>
    protected async Task<(User? User, IActionResult? Error)> RequireAsync(bool admin)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return (null, Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Not signed in"));
        }
        if (Account.Status(user) != Account.Active || (admin && !Account.IsAdmin(user)))
        {
            return (null, Problem(statusCode: StatusCodes.Status403Forbidden,
                detail: admin ? "Administrators only" : "The account is not active"));
        }
        return (user, null);
    }

    /// <summary>The signed-in user as GET /api/auth/me returns it (same fields as Sportlab).</summary>
    protected static UserOut ToUser(User user) =>
        new(user.UserId, user.Email, Account.DisplayName(user), Account.Status(user), Account.Role(user));

    /// <summary>A user as administrators see it in the user list.</summary>
    protected static UserAdminOut ToAdminUser(User user) => new(
        user.UserId, user.Email, Account.DisplayName(user), Account.Status(user), Account.Role(user),
        Account.Get(user, Account.CreatedAt), Account.Get(user, Account.LastLoginAt),
        Account.Get(user, Account.ApprovedAt), Account.ApprovedBy(user));
}

public sealed record UserOut(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email,
    [property: System.Text.Json.Serialization.JsonPropertyName("display_name")] string? DisplayName,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("role")] string Role);

public sealed record UserAdminOut(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email,
    [property: System.Text.Json.Serialization.JsonPropertyName("display_name")] string? DisplayName,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("role")] string Role,
    [property: System.Text.Json.Serialization.JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("last_login_at")] DateTimeOffset? LastLoginAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("approved_by")] string? ApprovedBy);
