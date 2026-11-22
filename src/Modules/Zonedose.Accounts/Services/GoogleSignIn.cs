using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace Zonedose.Accounts.Services;

public enum SignInOutcome
{
    SignedIn,
    Disabled,
    Refused,
}

/// <summary>The verified claims of a Google sign-in that Zonedose uses.</summary>
public sealed record GoogleIdentity(string Sub, string Email, string? DisplayName);

/// <summary>
/// The user for a verified Google identity, created on the first sign-in.
///
/// Identity is Google's <c>sub</c> (an Orchard external login), never the email:
/// a Google account can change address. A new account is pending, unless its
/// email is ADMIN_EMAIL: then it is an active administrator. This happens only
/// at creation, so an administrator demoted by another one does not become admin
/// again by signing in.
///
/// Orchard user names are generated (<c>user-…</c>), never the email, so Orchard's
/// own log lines carry no personal data. Our log lines carry the user id only.
/// </summary>
public sealed class GoogleSignIn(
    UserManager<IUser> userManager,
    IUserService userService,
    IOptions<AccountSettings> settings,
    TimeProvider time,
    ILogger<GoogleSignIn> logger)
{
    /// <returns>The user; <see cref="SignInOutcome.Disabled"/> for a disabled account;
    /// <see cref="SignInOutcome.Refused"/> when the account cannot be created
    /// (for example, its email already belongs to another account).</returns>
    public async Task<(User? User, SignInOutcome Outcome)> SignInAsync(GoogleIdentity identity)
    {
        var now = time.GetUtcNow();
        var email = identity.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByLoginAsync(Account.GoogleProvider, identity.Sub) is User existing)
        {
            if (!existing.IsEnabled)
            {
                logger.LogInformation("Sign-in refused for disabled user {UserId}", existing.UserId);
                return (null, SignInOutcome.Disabled);
            }
            existing.Email = email;
            Account.SetDisplayName(existing, identity.DisplayName);
            Account.Set(existing, Account.LastLoginAt, now);
            await userManager.UpdateAsync(existing);
            return (existing, SignInOutcome.SignedIn);
        }

        var isAdmin = settings.Value.NormalizedAdminEmail is { } adminEmail && email == adminEmail;
        var user = new User
        {
            UserName = "user-" + Guid.NewGuid().ToString("N")[..12],
            Email = email,
            EmailConfirmed = true,
        };
        Account.SetStatus(user, isAdmin ? Account.Active : Account.Pending);
        Account.SetDisplayName(user, identity.DisplayName);
        Account.Set(user, Account.CreatedAt, now);
        Account.Set(user, Account.LastLoginAt, now);

        var errors = new List<string>();
        if (await userService.CreateUserAsync(user, null, (_, message) => errors.Add(message)) is not User created)
        {
            // Error texts may quote the email: log only that it failed
            logger.LogWarning("A new Google account could not be created ({Count} validation errors)", errors.Count);
            return (null, SignInOutcome.Refused);
        }
        await userManager.AddLoginAsync(created, new UserLoginInfo(Account.GoogleProvider, identity.Sub, Account.GoogleProvider));
        if (isAdmin)
        {
            await userManager.AddToRoleAsync(created, Account.AdminRole);
        }
        logger.LogInformation("User {UserId} created as {Status}", created.UserId, Account.Status(created));
        return (created, SignInOutcome.SignedIn);
    }
}
