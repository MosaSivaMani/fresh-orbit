using AwesomeAssertions;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Promotions.Tests.Integration;

/// <summary>Banner columns against real Postgres.</summary>
public sealed class BannerPersistenceTests(PostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 6, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Banner_badge_round_trips()
    {
        var banner = Promotion.Create(new PromotionDefinition("Prawn week", null, null, PromotionLinkType.Coupon, "WELCOME100", null, Now, null, 10, true, "20% OFF"));
        await using (var db = database.CreateDbContext())
        {
            db.Add(banner);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        (await read.Set<Promotion>().SingleAsync(p => p.Id == banner.Id, Ct)).Badge.Should().Be("20% OFF");
    }
}
