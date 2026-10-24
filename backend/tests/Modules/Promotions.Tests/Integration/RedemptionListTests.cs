using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using static FreshOrbit.Promotions.Tests.Unit.CouponRuleTests;

namespace FreshOrbit.Promotions.Tests.Integration;

/// <summary>G16: redemption rows carry the order number, read from Ordering.Public in one batch per page.</summary>
public sealed class RedemptionListTests(PostgresFixture database) : IAsyncLifetime
{
    private readonly RedemptionApiFactory _factory = new(database);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Redemptions_show_the_order_number_and_null_for_an_unknown_order()
    {
        var coupon = Coupon.Create(Definition(code: "R" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()));
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.Add(coupon);
            db.Add(CouponRedemption.Redeem(coupon, Guid.NewGuid(), known, 10000, false, Now));
            db.Add(CouponRedemption.Redeem(coupon, Guid.NewGuid(), unknown, 10000, false, Now.AddMinutes(1)));
            await db.SaveChangesAsync(Ct);
        }

        _factory.Ordering.GetOrderNumbersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [known] = "FO-260042" });
        using var staff = _factory.CreateStaffClient(["promotions.manage"], allLocations: true);

        var response = await staff.GetAsync(new Uri($"/api/v1/admin/coupons/{coupon.Id}/redemptions", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("orderId").GetGuid());
        items[known].GetProperty("orderNumber").GetString().Should().Be("FO-260042");
        items[unknown].GetProperty("orderNumber").ValueKind.Should().Be(JsonValueKind.Null);
        await _factory.Ordering.Received(1).GetOrderNumbersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(known) && ids.Contains(unknown)), Arg.Any<CancellationToken>());
    }

    private sealed class RedemptionApiFactory(PostgresFixture database) : ApiFactory(database)
    {
        public IOrderingApi Ordering { get; } = Substitute.For<IOrderingApi>();

        protected override void ConfigureTestServices(IServiceCollection services)
            => services.Replace(ServiceDescriptor.Singleton(Ordering));
    }
}
