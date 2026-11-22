using System.Net;
using System.Net.Http.Json;
using System.Web;

namespace Zonedose.Web.Tests;

/// <summary>The signed-in user, as GET /api/auth/me returns it.</summary>
public sealed record Me(string Id, string Email, string? Display_name, string Status, string Role);

/// <summary>
/// The outcome of a Google sign-in: the browser-like client, where it ended,
/// and the session cookie it received (name=value), if any.
/// </summary>
public sealed record SignIn(HttpClient Client, string Location, string? SessionCookie);

public static class GoogleSession
{
    public const string FinishPath = "/api/auth/google/finish";

    /// <summary>
    /// The whole Google sign-in, as a browser goes through it: our login
    /// endpoint redirects to Google; "Google" sends the browser back to the
    /// callback with a code and the state; the callback hands over to finish,
    /// which opens the session and redirects to the frontend.
    /// </summary>
    public static async Task<SignIn> SignInAsync(this OrchardFixture orchard, GoogleUser user, Func<string, string>? tamperState = null)
    {
        var client = orchard.ClientFor(OrchardFixture.SiteHost);
        var login = await client.GetAsync("/api/auth/google/login");
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        var google = login.Headers.Location!;
        Assert.Equal("accounts.google.com", google.Host);
        var state = HttpUtility.ParseQueryString(google.Query)["state"]!;
        state = tamperState?.Invoke(state) ?? state;

        var code = orchard.Google.Issue(user);
        var callback = await client.GetAsync($"/api/auth/google/callback?code={code}&state={Uri.EscapeDataString(state)}");
        var next = Location(callback);
        string? session = null;
        if (next == FinishPath)
        {
            var finish = await client.GetAsync(FinishPath);
            next = Location(finish);
            session = finish.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? cookies.FirstOrDefault(c => c.StartsWith("zonedose_session=", StringComparison.Ordinal))?.Split(';')[0]
                : null;
        }
        return new SignIn(client, next, session);
    }

    /// <summary>Signs in and checks that the sign-in succeeded.</summary>
    public static async Task<HttpClient> SignedInAsync(this OrchardFixture orchard, GoogleUser user)
    {
        var signIn = await orchard.SignInAsync(user);
        Assert.Equal("/", signIn.Location);
        return signIn.Client;
    }

    /// <summary>The ADMIN_EMAIL account (always the same Google account).</summary>
    public static Task<HttpClient> AdminAsync(this OrchardFixture orchard) =>
        orchard.SignedInAsync(new GoogleUser("sub-admin", OrchardFixture.AdminEmail));

    public static Task<Me?> MeAsync(this HttpClient client) =>
        client.GetFromJsonAsync<Me>("/api/auth/me");

    /// <summary>A state-changing request as the Angular app sends it (with the CSRF header).</summary>
    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return client.SendAsync(request);
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True((int)response.StatusCode is >= 300 and < 400, $"Expected a redirect, got {(int)response.StatusCode}");
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}
