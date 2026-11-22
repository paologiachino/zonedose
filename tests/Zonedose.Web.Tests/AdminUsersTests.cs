using System.Net;
using System.Net.Http.Json;

namespace Zonedose.Web.Tests;

/// <summary>User administration and the last-admin guard (Sportlab parity).</summary>
[Collection(OrchardCollection.Name)]
public sealed class AdminUsersTests(OrchardFixture orchard)
{
    private sealed record AdminUser(string Id, string Email, string Status, string Role, DateTimeOffset? Created_at, DateTimeOffset? Approved_at, string? Approved_by);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_lists_users_by_status()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var pending = await (await orchard.SignedInAsync(GoogleUser.New())).MeAsync();

        var list = await admin.GetFromJsonAsync<AdminUser[]>("/api/admin/users?status=pending", Token);

        Assert.Contains(list!, u => u.Id == pending!.Id);
        Assert.All(list!, u => Assert.Equal("pending", u.Status));
        Assert.NotNull(list!.Single(u => u.Id == pending!.Id).Created_at);
    }

    [Fact]
    public async Task Approval_activates_the_user_and_records_who_approved()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var adminId = (await admin.MeAsync())!.Id;
        var user = await orchard.SignedInAsync(GoogleUser.New());
        var id = (await user.MeAsync())!.Id;

        var response = await admin.PostAsync($"/api/admin/users/{id}/approve");
        var approved = await response.Content.ReadFromJsonAsync<AdminUser>(Token);

        Assert.Equal(("active", adminId), (approved!.Status, approved.Approved_by));
        Assert.NotNull(approved.Approved_at);
        Assert.Equal("active", (await user.MeAsync())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/users/{id}/approve")).StatusCode);
    }

    [Fact]
    public async Task Disabling_ends_the_sessions_at_once_and_blocks_sign_in()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var google = GoogleUser.New();
        var user = await orchard.SignedInAsync(google);
        var id = (await user.MeAsync())!.Id;

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/users/{id}/disable")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/auth/me", Token)).StatusCode);
        Assert.Equal("/login?error=disabled", (await orchard.SignInAsync(google)).Location);

        // Approving again lets the person back in
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/users/{id}/approve")).StatusCode);
        Assert.Equal("active", (await (await orchard.SignedInAsync(google)).MeAsync())!.Status);
    }

    [Fact]
    public async Task Only_active_users_can_be_promoted_and_roles_change_both_ways()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var user = await orchard.SignedInAsync(GoogleUser.New());
        var id = (await user.MeAsync())!.Id;

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/users/{id}/role", new { role = "admin" })).StatusCode);
        await admin.PostAsync($"/api/admin/users/{id}/approve");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/users/{id}/role", new { role = "admin" })).StatusCode);
        Assert.Equal("admin", (await user.MeAsync())!.Role);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/admin/users", Token)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/users/{id}/role", new { role = "user" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/users", Token)).StatusCode);
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_removed()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var adminId = (await admin.MeAsync())!.Id;
        // Leave the ADMIN_EMAIL account as the only active administrator
        var everyone = await admin.GetFromJsonAsync<AdminUser[]>("/api/admin/users?status=active", Token);
        foreach (var other in everyone!.Where(u => u.Role == "admin" && u.Id != adminId))
        {
            await admin.PostAsync($"/api/admin/users/{other.Id}/role", new { role = "user" });
        }

        var demote = await admin.PostAsync($"/api/admin/users/{adminId}/role", new { role = "user" });
        var disable = await admin.PostAsync($"/api/admin/users/{adminId}/disable");

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Contains("last active administrator", await demote.Content.ReadAsStringAsync(Token));
        Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
        Assert.Equal("admin", (await admin.MeAsync())!.Role);
    }

    [Fact]
    public async Task Two_admins_demoting_each_other_at_once_leave_one_admin()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var adminId = (await admin.MeAsync())!.Id;
        var everyone = await admin.GetFromJsonAsync<AdminUser[]>("/api/admin/users?status=active", Token);
        foreach (var other in everyone!.Where(u => u.Role == "admin" && u.Id != adminId))
        {
            await admin.PostAsync($"/api/admin/users/{other.Id}/role", new { role = "user" });
        }
        var second = await orchard.SignedInAsync(GoogleUser.New());
        var secondId = (await second.MeAsync())!.Id;
        await admin.PostAsync($"/api/admin/users/{secondId}/approve");
        await admin.PostAsync($"/api/admin/users/{secondId}/role", new { role = "admin" });

        var results = await Task.WhenAll(
            admin.PostAsync($"/api/admin/users/{secondId}/role", new { role = "user" }),
            second.PostAsync($"/api/admin/users/{adminId}/role", new { role = "user" }));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        var after = await admin.GetFromJsonAsync<AdminUser[]>("/api/admin/users?status=active", Token)
            ?? await second.GetFromJsonAsync<AdminUser[]>("/api/admin/users?status=active", Token);
        Assert.Single(after!, u => u.Role == "admin");
    }

    [Fact]
    public async Task Unknown_user_is_404_and_non_admins_get_403()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var admin = await orchard.AdminAsync();
        var user = await orchard.SignedInAsync(GoogleUser.New());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/users/nobody/approve")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/api/admin/users/nobody/approve")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await orchard.ClientFor(OrchardFixture.SiteHost).GetAsync("/api/admin/users", Token)).StatusCode);
    }
}
