using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Zonedose.Spike.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Google;
using OrchardCore.Google.Authentication.Services;
using OrchardCore.Google.Authentication.Settings;

namespace Zonedose.Spike.Migrations;

/// <summary>
/// Runs with whatever connection the tenant has when the feature is enabled or
/// upgraded. With the migrator role the table is owned by the migrator, so RLS
/// applies to the app role (not owner, no BYPASSRLS).
/// </summary>
public sealed class SpikeMigrations(
    ShellSettings shellSettings,
    IShellConfiguration configuration,
    GoogleAuthenticationService google,
    IDataProtectionProvider dataProtection) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        var table = ModuleTables.Activity(shellSettings);
        await using (var conn = new NpgsqlConnection(shellSettings["ConnectionString"]))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"""
                CREATE TABLE {table} (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    user_id text NOT NULL,
                    name text NOT NULL,
                    start_point public.geometry(Point, 4326) NOT NULL,
                    created_at timestamptz NOT NULL DEFAULT now()
                );
                CREATE INDEX ON {table} (user_id);
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                CREATE POLICY activity_owner ON {table}
                    USING (user_id = current_setting('zonedose.user_id', true))
                    WITH CHECK (user_id = current_setting('zonedose.user_id', true));
                """, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // Google client from configuration: secrets never go in a recipe file
        var clientId = configuration["ZonedoseSpike:GoogleClientId"];
        var clientSecret = configuration["ZonedoseSpike:GoogleClientSecret"];
        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
        {
            var protector = dataProtection.CreateProtector(GoogleConstants.Features.GoogleAuthentication);
            await google.UpdateSettingsAsync(new GoogleAuthenticationSettings
            {
                ClientID = clientId,
                ClientSecret = protector.Protect(clientSecret),
                // Same redirect URI already registered for sportlab's dev client
                CallbackPath = "/api/auth/google/callback",
            });
        }

        return 1;
    }

    /// <summary>A later schema change, to see what the app role does with it at startup.</summary>
    public async Task<int> UpdateFrom1Async()
    {
        await using var conn = new NpgsqlConnection(shellSettings["ConnectionString"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"ALTER TABLE {ModuleTables.Activity(shellSettings)} ADD COLUMN note text", conn);
        await cmd.ExecuteNonQueryAsync();
        return 2;
    }
}
