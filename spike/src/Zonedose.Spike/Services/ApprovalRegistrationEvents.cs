using Microsoft.AspNetCore.Identity;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace Zonedose.Spike.Services;

/// <summary>
/// Runs inside Orchard's UserService.RegisterAsync, which is also what the
/// external (Google) auto-registration calls. A new account is pending, unless
/// its email is the configured admin email: then it is active and Administrator.
/// Registration never assigns roles otherwise.
/// </summary>
public sealed class ApprovalRegistrationEvents(
    UserManager<IUser> userManager,
    IShellConfiguration configuration) : RegistrationFormEventsBase
{
    public override async Task RegisteringAsync(UserRegisteringContext context)
    {
        if (context.User is not User user)
        {
            return;
        }

        var adminEmail = configuration["ZonedoseSpike:AdminEmail"]?.Trim();
        var isAdmin = !string.IsNullOrEmpty(adminEmail)
            && string.Equals(user.Email?.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase);

        AccountStatus.Set(user, isAdmin ? AccountStatus.Active : AccountStatus.Pending);
        await userManager.UpdateAsync(user);
        if (isAdmin)
        {
            await userManager.AddToRoleAsync(user, AccountStatus.AdminRole);
        }
    }
}
