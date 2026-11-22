using System.Net;

namespace Zonedose.Web.Tests;

/// <summary>An Orchard of its own: these tests move the application's clock.</summary>
public sealed class OrchardClockFixture : OrchardFixture;

[CollectionDefinition(Name)]
public sealed class OrchardClockCollection : ICollectionFixture<OrchardClockFixture>
{
    public const string Name = "OrchardClock";
}

/// <summary>Session lifetimes, as in Sportlab: 30 days sliding, 90 days at most.</summary>
[Collection(OrchardClockCollection.Name)]
public sealed class SessionLifetimeTests(OrchardClockFixture orchard)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unused_session_ends_after_30_days()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = await orchard.SignedInAsync(GoogleUser.New());

        orchard.Clock.Advance(TimeSpan.FromDays(29));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Token)).StatusCode);

        orchard.Clock.Advance(TimeSpan.FromDays(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Token)).StatusCode);
    }

    [Fact]
    public async Task A_used_session_still_ends_90_days_after_sign_in()
    {
        Assert.SkipWhen(OrchardFixture.Skip, OrchardFixture.SkipReason);
        var client = await orchard.SignedInAsync(GoogleUser.New());

        // Used every 20 days: the sliding window never closes...
        for (var day = 20; day <= 80; day += 20)
        {
            orchard.Clock.Advance(TimeSpan.FromDays(20));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Token)).StatusCode);
        }

        // ...but the session started 100 days ago
        orchard.Clock.Advance(TimeSpan.FromDays(20));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Token)).StatusCode);
    }
}
