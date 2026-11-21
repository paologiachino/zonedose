using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Zonedose.Spike.Services;

namespace Zonedose.Spike.Controllers;

[Route("api/admin/users")]
public sealed class AdminUsersController(UserManager<IUser> userManager) : ApiControllerBase(userManager)
{
    [HttpPost("{userId}/status")]
    public async Task<IActionResult> SetStatus(string userId, [FromBody] StatusChange body)
    {
        var (_, error) = await RequireActiveAsync(admin: true);
        if (error is not null)
        {
            return error;
        }
        if (await Users.FindByIdAsync(userId) is not User target)
        {
            return NotFound();
        }
        AccountStatus.Set(target, body.Status);
        await Users.UpdateAsync(target);
        return Ok(ToDto(target));
    }

    public sealed record StatusChange(string Status);
}
