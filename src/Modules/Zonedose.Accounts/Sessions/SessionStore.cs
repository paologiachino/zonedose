using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Npgsql;
using OrchardCore.Environment.Shell;

namespace Zonedose.Accounts.Sessions;

/// <summary>
/// Server-side sessions for the sign-in cookie (ASP.NET's ITicketStore).
///
/// The cookie carries only a random key; the session itself is a row here, so:
/// <list type="bullet">
/// <item>logout deletes the row, and a copied cookie stops working at once;</item>
/// <item>disabling a user deletes all their rows (<see cref="RemoveAllAsync"/>);</item>
/// <item>a session ends after <c>SessionMaxDays</c> from its sign-in whatever the
/// activity, on top of the sliding <c>SessionIdleDays</c> of the cookie.</item>
/// </list>
/// Only a SHA-256 hash of the key is stored: a copy of the table opens no session.
/// </summary>
public sealed class SessionStore : ITicketStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _table;
    private readonly TimeProvider _time;
    private readonly AccountSettings _settings;

    public SessionStore(ShellSettings shellSettings, TimeProvider time, IOptions<AccountSettings> settings)
    {
        _dataSource = NpgsqlDataSource.Create(shellSettings["ConnectionString"]!);
        _table = SessionTable.Name(shellSettings);
        _time = time;
        _settings = settings.Value;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = _time.GetUtcNow();
        await using var cmd = _dataSource.CreateCommand(
            $"INSERT INTO {_table} (key_hash, user_id, ticket, created_at, expires_at) VALUES ($1, $2, $3, $4, $5)");
        cmd.Parameters.AddWithValue(Hash(key));
        cmd.Parameters.AddWithValue(UserId(ticket));
        cmd.Parameters.AddWithValue(TicketSerializer.Default.Serialize(ticket));
        cmd.Parameters.AddWithValue(now);
        cmd.Parameters.AddWithValue(Expiry(ticket, now));
        await cmd.ExecuteNonQueryAsync();
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Sliding renewal never moves the absolute end (created_at is kept)
        await using var cmd = _dataSource.CreateCommand(
            $"UPDATE {_table} SET ticket = $2, expires_at = $3 WHERE key_hash = $1");
        cmd.Parameters.AddWithValue(Hash(key));
        cmd.Parameters.AddWithValue(TicketSerializer.Default.Serialize(ticket));
        cmd.Parameters.AddWithValue(Expiry(ticket, _time.GetUtcNow()));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var now = _time.GetUtcNow();
        await using var cmd = _dataSource.CreateCommand(
            $"SELECT ticket, created_at, expires_at FROM {_table} WHERE key_hash = $1");
        cmd.Parameters.AddWithValue(Hash(key));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }
        var ticket = TicketSerializer.Default.Deserialize(reader.GetFieldValue<byte[]>(0));
        var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
        await reader.CloseAsync();

        if (ticket is null || now >= expiresAt || now >= createdAt.AddDays(_settings.SessionMaxDays))
        {
            await RemoveAsync(key);
            return null;
        }
        return ticket;
    }

    public async Task RemoveAsync(string key)
    {
        await using var cmd = _dataSource.CreateCommand($"DELETE FROM {_table} WHERE key_hash = $1");
        cmd.Parameters.AddWithValue(Hash(key));
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Ends every session of a user (disabled, or signed out everywhere).</summary>
    public async Task<int> RemoveAllAsync(string userId)
    {
        await using var cmd = _dataSource.CreateCommand($"DELETE FROM {_table} WHERE user_id = $1");
        cmd.Parameters.AddWithValue(userId);
        return await cmd.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private static byte[] Hash(string key) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));

    private static string UserId(AuthenticationTicket ticket) =>
        ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("A session needs a user id.");

    private DateTimeOffset Expiry(AuthenticationTicket ticket, DateTimeOffset now) =>
        ticket.Properties.ExpiresUtc ?? now.AddDays(_settings.SessionIdleDays);
}

/// <summary>
/// The sign-in cookie of the tenant: server-side session, HttpOnly, SameSite=Strict,
/// Secure and __Host- prefixed outside development, sliding <c>SessionIdleDays</c>.
/// API callers get 401/403 instead of a redirect to a sign-in page.
/// </summary>
public sealed class ConfigureSessionCookie(
    SessionStore store,
    IOptions<AccountSettings> settings,
    Microsoft.Extensions.Hosting.IHostEnvironment environment) : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public const string DevelopmentName = "zonedose_session";
    public const string SecureName = "__Host-zonedose_session";

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name != Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme)
        {
            return;
        }
        var development = Microsoft.Extensions.Hosting.HostEnvironmentEnvExtensions.IsDevelopment(environment);

        options.SessionStore = store;
        options.Cookie.Name = development ? DevelopmentName : SecureName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
        options.Cookie.SecurePolicy = development
            ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
            : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        // __Host- requires Path=/ and no Domain
        options.Cookie.Path = "/";
        options.Cookie.Domain = null;
        options.ExpireTimeSpan = TimeSpan.FromDays(settings.Value.SessionIdleDays);
        options.SlidingExpiration = true;

        var toLogin = options.Events.OnRedirectToLogin;
        var toDenied = options.Events.OnRedirectToAccessDenied;
        options.Events.OnRedirectToLogin = context => IsApi(context.Request)
            ? Status(context.Response, Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized)
            : toLogin(context);
        options.Events.OnRedirectToAccessDenied = context => IsApi(context.Request)
            ? Status(context.Response, Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden)
            : toDenied(context);
    }

    private static bool IsApi(Microsoft.AspNetCore.Http.HttpRequest request) => request.Path.StartsWithSegments("/api");

    private static Task Status(Microsoft.AspNetCore.Http.HttpResponse response, int code)
    {
        response.StatusCode = code;
        return Task.CompletedTask;
    }
}
