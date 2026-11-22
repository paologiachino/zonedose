using System.Net;
using System.Net.Http.Json;

namespace Zonedose.Web.Tests;

[Collection(OrchardCollection.Name)]
public sealed class SetupTests(OrchardFixture orchard)
{
    private sealed record Health(string Status, string Tenant);

    [Fact]
    public async Task Tenants_are_set_up_from_configuration_and_answer_on_their_host()
    {
        Assert.SkipWhen(OrchardFixture.Connection is null, "ZONEDOSE_TEST_DB_CONNECTION is not set");
        await orchard.EnsureSetUpAsync();

        using var site = orchard.ClientFor(OrchardFixture.SiteHost);
        using var admin = orchard.ClientFor(OrchardFixture.AdminHost);

        var siteHealth = await site.GetFromJsonAsync<Health>("/api/health", TestContext.Current.CancellationToken);
        var adminHealth = await admin.GetFromJsonAsync<Health>("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(new Health("ok", "zonedose"), siteHealth);
        Assert.Equal(new Health("ok", "Default"), adminHealth);
    }

    [Fact]
    public async Task Unknown_host_gets_no_tenant()
    {
        Assert.SkipWhen(OrchardFixture.Connection is null, "ZONEDOSE_TEST_DB_CONNECTION is not set");
        await orchard.EnsureSetUpAsync();

        using var client = orchard.ClientFor("unknown.example.test");
        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_recipe_enables_users_and_the_admin()
    {
        Assert.SkipWhen(OrchardFixture.Connection is null, "ZONEDOSE_TEST_DB_CONNECTION is not set");
        await orchard.EnsureSetUpAsync();

        using var site = orchard.ClientFor(OrchardFixture.SiteHost);
        var login = await site.GetAsync("/Login", TestContext.Current.CancellationToken);
        var admin = await site.GetAsync("/Admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        // The admin exists and asks anonymous visitors to sign in
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.StartsWith("/Login", admin.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Site_recipe_enables_content_types()
    {
        Assert.SkipWhen(OrchardFixture.Connection is null, "ZONEDOSE_TEST_DB_CONNECTION is not set");
        await orchard.EnsureSetUpAsync();

        using var site = orchard.ClientFor(OrchardFixture.SiteHost);
        var response = await site.GetAsync("/Admin/ContentTypes/List", TestContext.Current.CancellationToken);

        // Redirect to sign-in, not 404: the ContentTypes feature is enabled
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
