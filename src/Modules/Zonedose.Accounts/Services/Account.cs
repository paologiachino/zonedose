using System.Text.Json.Nodes;
using OrchardCore.Users.Models;

namespace Zonedose.Accounts.Services;

/// <summary>
/// Zonedose's account data on top of an Orchard user.
///
/// <list type="bullet">
/// <item><c>disabled</c> is Orchard's <c>IsEnabled = false</c>: Orchard itself then
/// refuses every sign-in, the admin included.</item>
/// <item><c>pending</c> and <c>active</c> are a status in the user's properties, so a
/// pending user can sign in and see the waiting page. A missing status reads as
/// pending (for example the local setup-admin).</item>
/// <item><c>admin</c> is Orchard's Administrator role.</item>
/// </list>
/// </summary>
public static class Account
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Disabled = "disabled";
    public const string AdminRole = "Administrator";
    public const string GoogleProvider = "Google";

    private const string Key = "ZonedoseAccount";

    public static string Status(User user) =>
        !user.IsEnabled ? Disabled : Data(user)["Status"]?.GetValue<string>() == Active ? Active : Pending;

    public static bool IsAdmin(User user) =>
        user.RoleNames.Any(r => string.Equals(r, AdminRole, StringComparison.OrdinalIgnoreCase));

    public static string Role(User user) => IsAdmin(user) ? "admin" : "user";

    public static bool IsActiveAdmin(User user) => Status(user) == Active && IsAdmin(user);

    public static void SetStatus(User user, string status)
    {
        user.IsEnabled = status != Disabled;
        if (status != Disabled)
        {
            Data(user)["Status"] = status;
        }
    }

    public static string? DisplayName(User user) => Data(user)["DisplayName"]?.GetValue<string>();

    public static void SetDisplayName(User user, string? name) => Data(user)["DisplayName"] = name;

    public static DateTimeOffset? Get(User user, string field) =>
        Data(user)[field]?.GetValue<DateTimeOffset>();

    public static void Set(User user, string field, DateTimeOffset value) => Data(user)[field] = value;

    public static string? ApprovedBy(User user) => Data(user)["ApprovedBy"]?.GetValue<string>();

    public static void SetApprovedBy(User user, string userId) => Data(user)["ApprovedBy"] = userId;

    public const string CreatedAt = "CreatedAt";
    public const string LastLoginAt = "LastLoginAt";
    public const string ApprovedAt = "ApprovedAt";

    private static JsonObject Data(User user)
    {
        if (user.Properties[Key] is not JsonObject data)
        {
            data = [];
            user.Properties[Key] = data;
        }
        return data;
    }
}
