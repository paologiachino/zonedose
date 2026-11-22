using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using Zonedose.Accounts.Sessions;

namespace Zonedose.Accounts.Services;

/// <summary>The action does not fit the user's current state (HTTP 409).</summary>
public class AdminActionException(string message) : Exception(message);

/// <summary>The user does not exist (HTTP 404).</summary>
public sealed class UserNotFoundException() : Exception("User not found");

/// <summary>
/// User administration: approve, disable, promote and demote.
///
/// The last active administrator can be neither disabled nor demoted. Every
/// action runs under one lock and saves before releasing it, so two
/// administrators demoting each other at the same moment cannot leave the site
/// with none: the second action waits and then counts again. The lock is
/// Orchard's distributed lock, in process unless Redis is configured: enough for
/// the single instance the homelab runs (more instances need Redis).
/// </summary>
public sealed class UserAdministration(
    UserManager<IUser> userManager,
    ISession session,
    IDistributedLock distributedLock,
    SessionStore sessions,
    TimeProvider time,
    ILogger<UserAdministration> logger)
{
    private const string LockKey = "zonedose-user-administration";

    public async Task<IReadOnlyList<User>> ListAsync(string? status)
    {
        var users = await session.Query<User, UserIndex>().OrderBy(u => u.DocumentId).ListAsync();
        return [.. users.Where(u => status is null || Account.Status(u) == status)
            .OrderBy(u => Account.Get(u, Account.CreatedAt) ?? DateTimeOffset.MinValue)];
    }

    /// <summary>Pending or disabled -> active; records who approved and when.</summary>
    public Task<User> ApproveAsync(User actor, string userId) => UnderLockAsync(userId, async user =>
    {
        if (Account.Status(user) == Account.Active)
        {
            throw new AdminActionException("the user is already active");
        }
        Account.SetStatus(user, Account.Active);
        Account.Set(user, Account.ApprovedAt, time.GetUtcNow());
        Account.SetApprovedBy(user, actor.UserId);
        await SaveAsync(user);
        logger.LogInformation("User {UserId} approved by {ActorId}", user.UserId, actor.UserId);
    });

    /// <summary>Pending or active -> disabled, and every session of the user ends.</summary>
    public Task<User> DisableAsync(User actor, string userId) => UnderLockAsync(userId, async user =>
    {
        if (Account.Status(user) == Account.Disabled)
        {
            throw new AdminActionException("the user is already disabled");
        }
        await GuardLastAdminAsync(user);
        Account.SetStatus(user, Account.Disabled);
        await SaveAsync(user);
        // Orchard re-checks the security stamp of signed-in users; our sessions end at once
        await userManager.UpdateSecurityStampAsync(user);
        await sessions.RemoveAllAsync(user.UserId);
        logger.LogInformation("User {UserId} disabled by {ActorId}", user.UserId, actor.UserId);
    });

    /// <summary>Promote to admin (active users only) or demote to user (not the last admin).</summary>
    public Task<User> SetRoleAsync(User actor, string userId, string role) => UnderLockAsync(userId, async user =>
    {
        if (role is not ("admin" or "user"))
        {
            throw new AdminActionException("role must be admin or user");
        }
        if (Account.Role(user) == role)
        {
            throw new AdminActionException($"the user is already {role}");
        }
        if (role == "admin")
        {
            if (Account.Status(user) != Account.Active)
            {
                throw new AdminActionException("only active users can become administrators");
            }
            await userManager.AddToRoleAsync(user, Account.AdminRole);
        }
        else
        {
            await GuardLastAdminAsync(user);
            await userManager.RemoveFromRoleAsync(user, Account.AdminRole);
        }
        // Orchard's admin reads roles from the sign-in cookie: refresh it
        await userManager.UpdateSecurityStampAsync(user);
        await session.SaveChangesAsync();
        logger.LogInformation("User {UserId} set to {Role} by {ActorId}", user.UserId, role, actor.UserId);
    });

    private async Task<User> UnderLockAsync(string userId, Func<User, Task> action)
    {
        var (locker, locked) = await distributedLock.TryAcquireLockAsync(LockKey, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        if (!locked)
        {
            throw new AdminActionException("another administration change is in progress, try again");
        }
        await using (locker)
        {
            if (await userManager.FindByIdAsync(userId) is not User user)
            {
                throw new UserNotFoundException();
            }
            await action(user);
            return user;
        }
    }

    private async Task GuardLastAdminAsync(User target)
    {
        if (!Account.IsActiveAdmin(target))
        {
            return;
        }
        var admins = await userManager.GetUsersInRoleAsync(Account.AdminRole);
        if (admins.OfType<User>().Count(Account.IsActiveAdmin) <= 1)
        {
            throw new AdminActionException("the last active administrator cannot be removed");
        }
    }

    private async Task SaveAsync(User user)
    {
        await userManager.UpdateAsync(user);
        // Saved before the lock is released, not at the end of the request
        await session.SaveChangesAsync();
    }
}
