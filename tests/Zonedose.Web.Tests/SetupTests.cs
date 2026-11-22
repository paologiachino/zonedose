using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Zonedose.Web.Tests;

[Collection(OrchardCollection.Name)]
public sealed class SetupTests(OrchardFixture orchard)
{
    private sealed record Health(string Status, string Tenant);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Migrate_step_sets_up_every_tenant_with_nothing_pending()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        Assert.Equal(["Default", "zonedose"], orchard.Migration.Select(m => m.Tenant));
        Assert.All(orchard.Migration, m =>
        {
            Assert.True(m.Running, $"{m.Tenant} is not running");
            Assert.Empty(m.MissingFeatures);
            Assert.Empty(m.PendingFeatures);
        });
    }

    [Fact]
    public void Recipe_features_are_read_from_the_module_recipes()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var application = orchard.Factory.Services
            .GetRequiredService<OrchardCore.Modules.IApplicationContext>().Application;

        var site = MigrationRunner.RecipeFeatures(application, "Zonedose");
        var host = MigrationRunner.RecipeFeatures(application, "ZonedoseHost");

        Assert.Contains("Zonedose.Accounts", site);
        Assert.Contains("OrchardCore.Users", site);
        Assert.Contains("OrchardCore.Tenants", host);
        Assert.DoesNotContain("Zonedose.Accounts", host);
    }

    [Theory]
    [InlineData(OrchardFixture.PlatformHost, "Default")]
    [InlineData(OrchardFixture.SiteHost, "zonedose")]
    [InlineData(OrchardFixture.AdminHost, "zonedose")]
    public async Task Each_host_reaches_its_tenant_and_it_is_ready(string host, string tenant)
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var client = orchard.ClientFor(host);
        var health = await client.GetFromJsonAsync<Health>("/api/health", Token);

        Assert.Equal(new Health("ok", tenant), health);
    }

    [Fact]
    public async Task Unknown_host_gets_no_tenant()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var client = orchard.ClientFor("unknown.example.test");
        var response = await client.GetAsync("/api/health", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/admin/ContentTypes/List")]
    [InlineData("/Login")]
    [InlineData("/Register")]
    [InlineData("/")]
    public async Task Site_host_serves_the_api_only(string path)
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var site = orchard.ClientFor(OrchardFixture.SiteHost);

        Assert.Equal(HttpStatusCode.NotFound, (await site.GetAsync(path, Token)).StatusCode);
    }

    [Fact]
    public async Task Local_sign_in_page_is_on_the_admin_host()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var admin = orchard.ClientFor(OrchardFixture.AdminHost);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Login", Token)).StatusCode);
    }

    [Theory]
    [InlineData(OrchardFixture.AdminHost)]
    [InlineData(OrchardFixture.PlatformHost)]
    public async Task Admin_asks_to_sign_in_on_the_admin_host(string host)
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var admin = orchard.ClientFor(host);
        var response = await admin.GetAsync("/Admin", Token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Site_recipe_enables_content_types()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var admin = orchard.ClientFor(OrchardFixture.AdminHost);
        var response = await admin.GetAsync("/Admin/ContentTypes/List", Token);

        // Redirect to sign-in, not 404: the ContentTypes feature is enabled
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Application_connects_with_the_app_role()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);

        using var site = orchard.ClientFor(OrchardFixture.SiteHost);
        (await site.GetAsync("/api/health", Token)).EnsureSuccessStatusCode();

        // The app role owns no table and cannot create one
        await using var conn = new NpgsqlConnection(OrchardFixture.AppConnection);
        await conn.OpenAsync(Token);
        await using var create = new NpgsqlCommand("CREATE TABLE zonedose_should_not_exist (id int)", conn);
        var error = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync(Token));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }
}
