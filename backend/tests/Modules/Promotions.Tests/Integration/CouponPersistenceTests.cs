using AwesomeAssertions;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using static FreshOrbit.Promotions.Tests.Unit.CouponRuleTests;

namespace FreshOrbit.Promotions.Tests.Integration;

/// <summary>Coupon limits against real Postgres: the coupon row version serializes concurrent redemptions.</summary>
public sealed class CouponPersistenceTests(PostgresFixture database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Coupon_with_scopes_round_trips()
    {
        var coupon = Coupon.Create(Definition(code: Unique(), scopes: [new CouponScopeDefinition(CouponScopeType.Category, FishCategory)], perCustomer: 1, total: 10));
        await using (var db = database.CreateDbContext())
        {
            db.Add(coupon);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.Set<Coupon>().Include(c => c.Scopes).SingleAsync(c => c.Id == coupon.Id, Ct);
        loaded.Code.Should().Be(coupon.Code);
        loaded.Scopes.Should().ContainSingle().Which.ScopeId.Should().Be(FishCategory);
        loaded.DiscountType.Should().Be(DiscountType.Flat);
        loaded.Version.Should().NotBe(0u);
    }

    [Fact]
    public async Task Two_checkouts_racing_for_the_last_use_cannot_both_commit()
    {
        var coupon = Coupon.Create(Definition(code: Unique(), total: 1));
        await using (var db = database.CreateDbContext())
        {
            db.Add(coupon);
            await db.SaveChangesAsync(Ct);
        }

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        var a = await first.Set<Coupon>().SingleAsync(c => c.Id == coupon.Id, Ct);
        var b = await second.Set<Coupon>().SingleAsync(c => c.Id == coupon.Id, Ct);
        first.Add(CouponRedemption.Redeem(a, Guid.NewGuid(), Guid.NewGuid(), 10000, false, Now));
        second.Add(CouponRedemption.Redeem(b, Guid.NewGuid(), Guid.NewGuid(), 10000, false, Now));

        await first.SaveChangesAsync(Ct);
        var lost = () => second.SaveChangesAsync(Ct);

        await lost.Should().ThrowAsync<ConcurrentSaveException>();
        await using var check = database.CreateDbContext();
        (await check.Set<Coupon>().SingleAsync(c => c.Id == coupon.Id, Ct)).UsedCount.Should().Be(1);
        (await check.Set<CouponRedemption>().CountAsync(r => r.CouponId == coupon.Id, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task One_redemption_per_order()
    {
        var coupon = Coupon.Create(Definition(code: Unique()));
        var orderId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.Add(coupon);
            db.Add(CouponRedemption.Redeem(coupon, Guid.NewGuid(), orderId, 10000, false, Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var again = database.CreateDbContext();
        var loaded = await again.Set<Coupon>().SingleAsync(c => c.Id == coupon.Id, Ct);
        again.Add(CouponRedemption.Redeem(loaded, Guid.NewGuid(), orderId, 10000, false, Now));
        var duplicate = () => again.SaveChangesAsync(Ct);

        await duplicate.Should().ThrowAsync<Exception>();
    }

    private static string Unique() => "C" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
}
