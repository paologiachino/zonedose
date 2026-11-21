using OrchardCore.Users.Models;

namespace Zonedose.Spike.Services;

/// <summary>
/// Sportlab's three account states on top of an Orchard user.
///
/// Orchard only has IsEnabled. A disabled Orchard user cannot sign in at all,
/// but Sportlab signs pending users in so the Angular app can show the waiting
/// page. So: disabled = IsEnabled false; pending/active = enabled Orchard user
/// plus a status in the user's Properties. A missing status reads as pending,
/// the safe default (e.g. the local super user created by setup).
/// </summary>
public static class AccountStatus
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Disabled = "disabled";

    private const string Key = "ZonedoseStatus";
    public const string AdminRole = "Administrator";

    public static string Of(User user)
    {
        if (!user.IsEnabled)
        {
            return Disabled;
        }
        return user.Properties[Key]?.GetValue<string>() == Active ? Active : Pending;
    }

    public static void Set(User user, string status)
    {
        user.IsEnabled = status != Disabled;
        user.Properties[Key] = status == Disabled ? Pending : status;
    }

    public static string RoleOf(User user) =>
        user.RoleNames.Contains(AdminRole) ? "admin" : "user";
}
