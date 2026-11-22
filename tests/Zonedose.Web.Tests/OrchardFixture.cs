using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;


namespace Zonedose.Web.Tests;

/// <summary>
/// The real application against a real PostgreSQL, deployed as in production:
/// first the migrate step with the migrator role, then the application with
/// the app role (decision 0002).
///
/// Needs ZONEDOSE_TEST_DB_CONNECTION (migrator role) and
/// ZONEDOSE_TEST_DB_APP_CONNECTION (app role) on the development database.
/// Without them the tests are skipped locally; CI always sets them. Every run
/// uses its own table prefixes and App_Data folder, and drops its tables at the end.
/// </summary>
public class OrchardFixture : IAsyncLifetime
{
    public const string PlatformHost = "platform.localhost";
    public const string SiteHost = "localhost";
    public const string AdminHost = "admin.localhost";
    public const string AdminEmail = "admin@example.test";

    private readonly string _run = "t" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "zonedose-tests", Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program>? _factory;

    public static string? MigratorConnection => Variable("ZONEDOSE_TEST_DB_CONNECTION");
    public static string? AppConnection => Variable("ZONEDOSE_TEST_DB_APP_CONNECTION");
    public static bool Skip => MigratorConnection is null || AppConnection is null;
    public const string SkipReason = "ZONEDOSE_TEST_DB_CONNECTION and ZONEDOSE_TEST_DB_APP_CONNECTION are not set";

    /// <summary>Google, as seen by the application.</summary>
    public FakeGoogle Google { get; } = new();

    /// <summary>The application's clock, moved by the session lifetime tests.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Every log line of the application.</summary>
    public LogCapture Logs { get; } = new();

    /// <summary>What the migrate step reported, one entry per tenant.</summary>
    public IReadOnlyList<TenantMigration> Migration { get; private set; } = [];

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("No test database configured.");

    public async ValueTask InitializeAsync()
    {
        if (Skip)
        {
            return;
        }

        // Read by Program before the host is built, so they go in the environment
        Environment.SetEnvironmentVariable("ZONEDOSE_SETUP_ADMIN_PASSWORD", "Test-" + Guid.NewGuid().ToString("N") + "!");
        Environment.SetEnvironmentVariable("ORCHARD_APP_DATA", _appData);
        Environment.SetEnvironmentVariable("ZONEDOSE_GOOGLE_CLIENT_ID", "test-client.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("ZONEDOSE_GOOGLE_CLIENT_SECRET", "test-secret");
        Environment.SetEnvironmentVariable("ZONEDOSE_ADMIN_EMAIL", AdminEmail);

        // 1. Migrate step, with the migrator role
        Environment.SetEnvironmentVariable("ZONEDOSE_DB_CONNECTION", MigratorConnection);
        await using (var migrator = CreateFactory())
        {
            Migration = await MigrationRunner.RunAsync(
                migrator.Services,
                () => migrator.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }),
                migrator.Services.GetRequiredService<IReadOnlyList<TenantLayout>>(),
                NullLogger.Instance,
                TestContext.Current.CancellationToken);
        }

        // 2. The application, with the app role, on the same database and App_Data
        Environment.SetEnvironmentVariable("ZONEDOSE_DB_CONNECTION", AppConnection);
        _factory = CreateFactory();
        // Built now, while the environment variables above are this fixture's
        _ = _factory.Services;
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseTablePrefix", _run + "_host");
            builder.UseSetting("OrchardCore:OrchardCore_AutoSetup:Tenants:1:DatabaseTablePrefix", _run + "_zonedose");
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(Logs);
                logging.AddFilter<LogCapture>(null, LogLevel.Information);
            });
            // Host services are copied into every tenant
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.PostConfigure<GoogleOptions>("Google", options => options.Backchannel = new HttpClient(Google));
            });
        });

    /// <summary>
    /// A client for one host, keeping cookies like a browser and not following
    /// redirects. HTTPS, so cookies marked Secure are sent back too.
    /// </summary>
    public HttpClient ClientFor(string host) =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{host}/"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    /// <summary>A client that sends only the cookies a test sets by hand.</summary>
    public HttpClient RawClientFor(string host) =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{host}/"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (!Skip)
        {
            await DropTablesAsync();
        }
        if (Directory.Exists(_appData))
        {
            Directory.Delete(_appData, recursive: true);
        }
    }

    private async Task DropTablesAsync()
    {
        await using var conn = new NpgsqlConnection(MigratorConnection);
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

    private static string? Variable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}

[CollectionDefinition(Name)]
public sealed class OrchardCollection : ICollectionFixture<OrchardFixture>
{
    public const string Name = "Orchard";
}
