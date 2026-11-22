using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;
using Zonedose.Accounts.Services;

namespace Zonedose.Accounts.Controllers;

/// <summary>
/// User administration, for active administrators only (same contract as Sportlab):
///
/// <list type="bullet">
/// <item>GET  /api/admin/users?status=pending: users, oldest first, optionally by status.</item>
/// <item>POST /api/admin/users/{userId}/approve: pending or disabled -> active.</item>
/// <item>POST /api/admin/users/{userId}/disable: -> disabled, sessions ended.</item>
/// <item>POST /api/admin/users/{userId}/role: {"role": "admin" | "user"}.</item>
/// </list>
/// 404 for an unknown user; 409 when the action does not fit the user's state,
/// including removing the last active administrator.
/// </summary>
[Route("api/admin/users")]
public sealed class AdminUsersController(UserManager<IUser> userManager, UserAdministration administration)
    : ApiControllerBase(userManager)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var (_, error) = await RequireAsync(admin: true);
        if (error is not null)
        {
            return error;
        }
        if (status is not (null or Account.Pending or Account.Active or Account.Disabled))
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, detail: "status must be pending, active or disabled");
        }
        return Ok((await administration.ListAsync(status)).Select(ToAdminUser));
    }

    [HttpPost("{userId}/approve")]
    public Task<IActionResult> Approve(string userId) =>
        RunAsync(actor => administration.ApproveAsync(actor, userId));

    [HttpPost("{userId}/disable")]
    public Task<IActionResult> Disable(string userId) =>
        RunAsync(actor => administration.DisableAsync(actor, userId));

    [HttpPost("{userId}/role")]
    public Task<IActionResult> SetRole(string userId, [FromBody] RoleChange body) =>
        RunAsync(actor => administration.SetRoleAsync(actor, userId, body.Role));

    private async Task<IActionResult> RunAsync(Func<OrchardCore.Users.Models.User, Task<OrchardCore.Users.Models.User>> action)
    {
        var (actor, error) = await RequireAsync(admin: true);
        if (error is not null)
        {
            return error;
        }
        try
        {
            return Ok(ToAdminUser(await action(actor!)));
        }
        catch (UserNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "User not found");
        }
        catch (AdminActionException exception)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: exception.Message);
        }
    }

    public sealed record RoleChange([property: System.Text.Json.Serialization.JsonPropertyName("role")] string Role);
}
