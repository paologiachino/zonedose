namespace Zonedose.Accounts;

/// <summary>
/// Settings of the accounts module, from the tenant configuration
/// (section Zonedose, filled by the host from ZONEDOSE_* variables).
/// </summary>
public sealed class AccountSettings
{
    public const string Section = "Zonedose:Accounts";

    /// <summary>Google OAuth client. Without it Google sign-in answers "unavailable".</summary>
    public string? GoogleClientId { get; set; }

    public string? GoogleClientSecret { get; set; }

    /// <summary>The account created with this verified email is an active administrator.</summary>
    public string? AdminEmail { get; set; }

    /// <summary>A session unused for this long ends (sliding).</summary>
    public int SessionIdleDays { get; set; } = 30;

    /// <summary>A session ends this long after its Google sign-in, whatever the activity.</summary>
    public int SessionMaxDays { get; set; } = 90;

    public bool GoogleConfigured =>
        !string.IsNullOrWhiteSpace(GoogleClientId) && !string.IsNullOrWhiteSpace(GoogleClientSecret);

    public string? NormalizedAdminEmail =>
        string.IsNullOrWhiteSpace(AdminEmail) ? null : AdminEmail.Trim().ToLowerInvariant();
}
