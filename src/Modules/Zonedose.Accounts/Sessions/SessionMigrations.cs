using Npgsql;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;

namespace Zonedose.Accounts.Sessions;

/// <summary>
/// The session table. Created by the migrate step (migrator role); the
/// application role only reads and writes rows (decision 0002). Not personal
/// data beyond the user id, so no Row-Level Security: only this module reads it.
/// </summary>
public sealed class SessionMigrations(ShellSettings shellSettings) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        var table = SessionTable.Name(shellSettings);
        await using var conn = new NpgsqlConnection(shellSettings["ConnectionString"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"""
            CREATE TABLE {table} (
                key_hash   bytea PRIMARY KEY,
                user_id    text NOT NULL,
                ticket     bytea NOT NULL,
                created_at timestamptz NOT NULL,
                expires_at timestamptz NOT NULL
            );
            CREATE INDEX ON {table} (user_id);
            """, conn);
        await cmd.ExecuteNonQueryAsync();
        return 1;
    }
}
