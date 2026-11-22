using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Zonedose.Web.Tests;

/// <summary>
/// The real application against a real PostgreSQL, set up from configuration
/// and recipes as in production.
///
/// Needs ZONEDOSE_TEST_DB_CONNECTION (a role that can create tables). Without
/// it the tests are skipped locally; CI always sets it. Every run uses its own
/// table prefixes and App_Data folder, and drops its tables at the end.
/// </summary>
public sealed class OrchardFixture : IAsyncLifetime
{
    public const string SiteHost = "localhost";
    public const string AdminHost = "admin.localhost";

    private readonly string _run = "t" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "zonedose-tests", Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program>? _factory;

    public static string? Connection =>
        Environment.GetEnvironmentVariable("ZONEDOSE_TEST_DB_CONNECTION") is { Length: > 0 } value ? value : null;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("No test database configured.");

    public ValueTask InitializeAsync()
    {
        if (string.IsNullOrEmpty(Connection))
        {
            return ValueTask.CompletedTask;
        }

        // Read by Program before the host is built, so they go in the environment
        Environment.SetEnvironmentVariable("ZONEDOSE_DB_CONNECTION", Connection);
        Environment.SetEnvironmentVariable("ZONEDOSE_SETUP_ADMIN_PASSWORD", "Test-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("ORCHARD_APP_DATA", _appData);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseTablePrefix", _run + "_host");
            builder.UseSetting("OrchardCore:OrchardCore_AutoSetup:Tenants:1:DatabaseTablePrefix", _run + "_zonedose");
        });
        return ValueTask.CompletedTask;
    }

    /// <summary>A client for one tenant: the Host header picks the tenant.</summary>
    public HttpClient ClientFor(string host)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Host = host;
        client.Timeout = TimeSpan.FromMinutes(3);
        return client;
    }

    /// <summary>
    /// The first request to the Default tenant runs its setup and creates the
    /// other tenants; the first request to each of them runs theirs.
    /// </summary>
    public async Task EnsureSetUpAsync()
    {
        foreach (var host in new[] { AdminHost, SiteHost })
        {
            using var client = ClientFor(host);
            var response = await client.GetAsync("/");
            if ((int)response.StatusCode >= 500)
            {
                throw new InvalidOperationException($"Setup of {host} failed: {(int)response.StatusCode}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            await DropTablesAsync();
        }
        if (Directory.Exists(_appData))
        {
            Directory.Delete(_appData, recursive: true);
        }
    }

    private async Task DropTablesAsync()
    {
        await using var conn = new NpgsqlConnection(Connection);
        await conn.OpenAsync();
        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE @prefix", conn))
        {
            list.Parameters.AddWithValue("prefix", _run + "\\_%");
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }
        foreach (var table in tables)
        {
            await using var drop = new NpgsqlCommand($"DROP TABLE IF EXISTS \"{table}\" CASCADE", conn);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class OrchardCollection : ICollectionFixture<OrchardFixture>
{
    public const string Name = "Orchard";
}
