using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;
using Zonedose.Spike.Services;

namespace Zonedose.Spike.Controllers;

/// <summary>The secured custom endpoint of the spike: an active user's own activities.</summary>
[Route("api/activities")]
public sealed class ActivitiesController(UserManager<IUser> userManager, ActivityStore store)
    : ApiControllerBase(userManager)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (user, error) = await RequireActiveAsync();
        return error ?? Ok(await store.ListAsync(user!.UserId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] NewActivity body, CancellationToken ct)
    {
        var (user, error) = await RequireActiveAsync();
        return error ?? Ok(new { id = await store.AddAsync(user!.UserId, body.Name, body.Lon, body.Lat, ct) });
    }

    /// <summary>RLS probe: rows visible with no WHERE clause, as me and as nobody.</summary>
    [HttpGet("rls-probe")]
    public async Task<IActionResult> RlsProbe(CancellationToken ct)
    {
        var (user, error) = await RequireActiveAsync();
        return error ?? Ok(new
        {
            dbRole = await store.CurrentRoleAsync(ct),
            visibleAsMe = await store.CountVisibleAsync(user!.UserId, ct),
            visibleWithNoUser = await store.CountVisibleAsync(null, ct),
        });
    }

    public sealed record NewActivity(string Name, double Lon, double Lat);
}
