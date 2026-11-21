using OrchardCore.Environment.Shell;

namespace Zonedose.Spike.Services;

/// <summary>Module table names follow the tenant's schema and table prefix, like Orchard's own.</summary>
public static class ModuleTables
{
    public static string Activity(ShellSettings settings)
    {
        var schema = string.IsNullOrEmpty(settings["Schema"]) ? "public" : settings["Schema"];
        var prefix = string.IsNullOrEmpty(settings["TablePrefix"]) ? "" : settings["TablePrefix"] + "_";
        return $"\"{schema}\".\"{prefix}zonedose_activity\"";
    }
}
