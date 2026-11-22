using System.Net;

namespace Zonedose.Web.Tests;

/// <summary>Google sign-in, approval rules and the session contract (Sportlab parity).</summary>
[Collection(OrchardCollection.Name)]
public sealed class AuthTests(OrchardFixture orchard)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_google_account_is_pending_and_can_only_read_itself()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var google = GoogleUser.New();

        var client = await orchard.SignedInAsync(google);
        var me = await client.MeAsync();

        Assert.Equal(google.Email, me!.Email);
        Assert.Equal("Test User", me.Display_name);
        Assert.Equal(("pending", "user"), (me.Status, me.Role));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users", Token)).StatusCode);
    }

    [Fact]
    public async Task Admin_email_account_is_created_as_active_administrator()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        var me = await (await orchard.AdminAsync()).MeAsync();

        Assert.Equal(("active", "admin"), (me!.Status, me.Role));
    }

    [Fact]
    public async Task Returning_user_keeps_status_and_role()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var google = GoogleUser.New();
        var first = await (await orchard.SignedInAsync(google)).MeAsync();

        var again = await (await orchard.SignedInAsync(google with { Email = "renamed-" + google.Email })).MeAsync();

        Assert.Equal(first!.Id, again!.Id);
        Assert.Equal("pending", again.Status);
        // The email follows Google's, the identity is the sub
        Assert.Equal("renamed-" + google.Email, again.Email);
    }

    [Fact]
    public async Task Unverified_email_is_refused_and_creates_no_account()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        var signIn = await orchard.SignInAsync(GoogleUser.New() with { EmailVerified = false });

        Assert.Equal("/login?error=unverified_email", signIn.Location);
        Assert.Equal(HttpStatusCode.Unauthorized, (await signIn.Client.GetAsync("/api/auth/me", Token)).StatusCode);
    }

    [Fact]
    public async Task Unverified_admin_email_does_not_become_administrator()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        var signIn = await orchard.SignInAsync(new GoogleUser("sub-" + Guid.NewGuid(), OrchardFixture.AdminEmail, EmailVerified: false));

        Assert.Equal("/login?error=unverified_email", signIn.Location);
    }

    [Fact]
    public async Task Tampered_state_is_refused()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        var signIn = await orchard.SignInAsync(GoogleUser.New(), state => state[..^4] + "AAAA");

        Assert.Equal("/login?error=failed", signIn.Location);
    }

    [Fact]
    public async Task Cancel_on_google_goes_back_to_the_login_page()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = orchard.ClientFor(OrchardFixture.SiteHost);
        var login = await client.GetAsync("/api/auth/google/login", Token);
        var state = System.Web.HttpUtility.ParseQueryString(login.Headers.Location!.Query)["state"]!;

        var callback = await client.GetAsync($"/api/auth/google/callback?error=access_denied&state={Uri.EscapeDataString(state)}", Token);

        Assert.Equal("/login?error=cancelled", callback.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Session_cookie_is_httponly_strict_and_persistent()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = orchard.ClientFor(OrchardFixture.SiteHost);
        var login = await client.GetAsync("/api/auth/google/login", Token);
        var state = System.Web.HttpUtility.ParseQueryString(login.Headers.Location!.Query)["state"]!;
        await client.GetAsync($"/api/auth/google/callback?code={orchard.Google.Issue(GoogleUser.New())}&state={Uri.EscapeDataString(state)}", Token);

        var finish = await client.GetAsync(GoogleSession.FinishPath, Token);
        var cookie = finish.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("zonedose_session=", StringComparison.Ordinal)).ToLowerInvariant();

        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/", cookie);
        Assert.Contains("expires=", cookie);
    }

    [Fact]
    public async Task Second_google_account_with_an_email_in_use_is_refused()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var first = GoogleUser.New();
        await orchard.SignedInAsync(first);

        var signIn = await orchard.SignInAsync(GoogleUser.New(first.Email));

        Assert.Equal("/login?error=failed", signIn.Location);
    }

    [Fact]
    public async Task Logout_ends_the_session_on_the_server()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var signIn = await orchard.SignInAsync(GoogleUser.New());
        var client = signIn.Client;
        var stolen = orchard.RawClientFor(OrchardFixture.SiteHost);
        stolen.DefaultRequestHeaders.Add("Cookie", signIn.SessionCookie!);
        Assert.Equal(HttpStatusCode.OK, (await stolen.GetAsync("/api/auth/me", Token)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Token)).StatusCode);
        // A copy of the cookie taken before the logout opens nothing either
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.GetAsync("/api/auth/me", Token)).StatusCode);
    }

    [Fact]
    public async Task Refresh_answers_204_while_signed_in_and_401_after()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = await orchard.SignedInAsync(GoogleUser.New());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/refresh")).StatusCode);
        await client.PostAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh")).StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = await orchard.SignedInAsync(GoogleUser.New());

        var response = await client.PostAsync("/api/auth/logout", null, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Token)).StatusCode);
    }

    [Fact]
    public async Task Me_without_a_session_is_401()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        var response = await orchard.ClientFor(OrchardFixture.SiteHost).GetAsync("/api/auth/me", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logs_carry_no_email()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var google = GoogleUser.New();

        await (await orchard.SignedInAsync(google)).MeAsync();

        Assert.DoesNotContain(orchard.Logs.Lines, line => line.Contains(google.Email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orchard.Logs.Lines, line => line.Contains(OrchardFixture.AdminEmail, StringComparison.OrdinalIgnoreCase));
    }
}
