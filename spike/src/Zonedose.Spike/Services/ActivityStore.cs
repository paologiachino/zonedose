using NetTopologySuite.Geometries;
using Npgsql;
using OrchardCore.Environment.Shell;

namespace Zonedose.Spike.Services;

public sealed record Activity(long Id, string Name, double Lon, double Lat, double DistanceToOriginKm);

/// <summary>
/// Personal data in a module-owned relational table, not in Orchard content items.
///
/// The module builds its own NpgsqlDataSource with NetTopologySuite from the
/// tenant connection string, so geometry columns map to NTS types. Every query
/// runs in a transaction that sets zonedose.user_id first: the RLS policy on the
/// table only shows that user's rows, as a second barrier after the WHERE clause.
/// </summary>
public sealed class ActivityStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _table;

    public ActivityStore(ShellSettings shellSettings)
    {
        var builder = new NpgsqlDataSourceBuilder(shellSettings["ConnectionString"]);
        builder.UseNetTopologySuite();
        _dataSource = builder.Build();
        _table = ModuleTables.Activity(shellSettings);
    }

    public string Table => _table;

    public async Task<IReadOnlyList<Activity>> ListAsync(string userId, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await SetUserAsync(conn, userId, ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, name, start_point, ST_Distance(start_point::geography, ST_MakePoint(0, 0)::geography) / 1000 " +
            $"FROM {_table} WHERE user_id = @uid ORDER BY id", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        var result = new List<Activity>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var point = reader.GetFieldValue<Point>(2);
                result.Add(new Activity(reader.GetInt64(0), reader.GetString(1), point.X, point.Y, reader.GetDouble(3)));
            }
        }
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<long> AddAsync(string userId, string name, double lon, double lat, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await SetUserAsync(conn, userId, ct);
        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO {_table} (user_id, name, start_point) VALUES (@uid, @name, @point) RETURNING id", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("point", new Point(lon, lat) { SRID = 4326 });
        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>
    /// RLS probe: counts all rows with no WHERE clause, as user <paramref name="userId"/>
    /// (or with no user set when null). Only the policy limits what is visible.
    /// </summary>
    public async Task<long> CountVisibleAsync(string? userId, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (userId is not null)
        {
            await SetUserAsync(conn, userId, ct);
        }
        await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {_table}", conn);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return count;
    }

    public async Task<string> CurrentRoleAsync(CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT current_user", conn);
        return (string)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task SetUserAsync(NpgsqlConnection conn, string userId, CancellationToken ct)
    {
        // set_config(..., true) is SET LOCAL: it ends with the transaction, so a
        // pooled connection never carries one user's id into another request
        await using var cmd = new NpgsqlCommand("SELECT set_config('zonedose.user_id', @uid, true)", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
