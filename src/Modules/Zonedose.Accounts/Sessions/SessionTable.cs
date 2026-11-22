using OrchardCore.Environment.Shell;

namespace Zonedose.Accounts.Sessions;

/// <summary>The module-owned session table, named like Orchard's tables for this tenant.</summary>
public static class SessionTable
{
    public static string Name(ShellSettings settings)
    {
        var prefix = settings["TablePrefix"];
        var name = string.IsNullOrEmpty(prefix) ? "zonedose_sessions" : $"{prefix}_zonedose_sessions";
        return $"\"{name.Replace("\"", "", StringComparison.Ordinal)}\"";
    }
}
