namespace Zonedose.Web;

/// <summary>
/// Maps the ZONEDOSE_* environment variables onto Orchard's configuration, so
/// appsettings.json holds the tenant layout and no secret.
///
/// <list type="bullet">
/// <item>ZONEDOSE_DB_CONNECTION: PostgreSQL connection for every tenant, used by
/// AutoSetup and, once a tenant exists, instead of the one Orchard stored in
/// App_Data (so the role can change without touching App_Data).</item>
/// <item>ZONEDOSE_SETUP_ADMIN_PASSWORD: password of the local super user that
/// Orchard creates in each tenant at setup.</item>
/// <item>ZONEDOSE_HOST_&lt;TENANT&gt;: hostname of a tenant (e.g. ZONEDOSE_HOST_ZONEDOSE).</item>
/// </list>
/// </summary>
public static class ZonedoseConfiguration
{
    private const string TenantsSection = "OrchardCore:OrchardCore_AutoSetup:Tenants";

    public static void Apply(ConfigurationManager configuration)
    {
        var connection = configuration["ZONEDOSE_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "ZONEDOSE_DB_CONNECTION is not set: see .env.example.");
        }
        var setupPassword = configuration["ZONEDOSE_SETUP_ADMIN_PASSWORD"];

        var values = new Dictionary<string, string?>();
        foreach (var tenant in configuration.GetSection(TenantsSection).GetChildren())
        {
            var name = tenant["ShellName"];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            values[$"{tenant.Path}:DatabaseConnectionString"] = connection;
            values[$"OrchardCore:{name}:ConnectionString"] = connection;
            if (!string.IsNullOrEmpty(setupPassword))
            {
                values[$"{tenant.Path}:AdminPassword"] = setupPassword;
            }
            var host = configuration[$"ZONEDOSE_HOST_{name.ToUpperInvariant()}"];
            if (!string.IsNullOrEmpty(host))
            {
                values[$"{tenant.Path}:RequestUrlHost"] = host;
            }
        }
        configuration.AddInMemoryCollection(values);
    }
}
