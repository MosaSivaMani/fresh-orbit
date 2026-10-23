using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Settings.Tests.Integration;

public sealed class SettingsApiTests(E1Database database) : E1ApiTestBase(database)
{
    [Fact]
    public async Task Number_series_is_gapless_under_parallel_callers()
    {
        const string period = "2031-32";
        await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            db.Add(NumberSeries.Create("ORDER", period, "T-", 4, 1));
            return await db.SaveChangesAsync(Ct);
        });

        const int callers = 25;
        var numbers = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(DrawOneAsync, Ct)));

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Order(StringComparer.Ordinal).Should().Equal(Enumerable.Range(1, callers).Select(i => $"T-{i:D4}"));
        var series = await QueryAsync(db => db.Set<NumberSeries>().SingleAsync(s => s.Code == "ORDER" && s.Period == period, Ct));
        series.NextValue.Should().Be(callers + 1);

        async Task<string> DrawOneAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var number = await scope.ServiceProvider.GetRequiredService<ISettingsApi>().NextNumberAsync("ORDER", period, Ct);
            await db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
            return number;
        }
    }

    [Fact]
    public async Task A_rolled_back_caller_does_not_consume_a_number()
    {
        const string period = "2032-33";
        await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            db.Add(NumberSeries.Create("INVOICE", period, "R-", 3, 1));
            return await db.SaveChangesAsync(Ct);
        });

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            (await scope.ServiceProvider.GetRequiredService<ISettingsApi>().NextNumberAsync("INVOICE", period, Ct)).Should().Be("R-001");
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        var next = await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var number = await provider.GetRequiredService<ISettingsApi>().NextNumberAsync("INVOICE", period, Ct);
            await db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
            return number;
        });
        next.Should().Be("R-001");
    }

    [Fact]
    public async Task Business_profile_is_created_once_then_updated_with_if_match()
    {
        using var admin = Factory.CreateStaffClient(["settings.manage"]);
        var first14 = "33AAACF1234A1Z";
        var body = new
        {
            legalName = "Fresh Orbit Private Limited",
            tradeName = "Fresh Orbit",
            gstin = first14 + Gstins.CheckCharacter(first14),
            fssaiLicenseNo = "12345678901234",
            address = "1 Anna Salai, Chennai 600002",
            supportPhone = "+914412345678",
            supportEmail = "support@freshorbit.in",
            timeZone = "Asia/Kolkata",
        };

        var noCondition = await admin.PutAsJsonAsync("/api/v1/admin/settings/business-profile", body, Ct);
        noCondition.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        var create = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/settings/business-profile") { Content = JsonContent.Create(body) };
        create.Headers.IfNoneMatch.ParseAdd("*");
        var created = await admin.SendAsync(create, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var badGstin = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/settings/business-profile") { Content = JsonContent.Create(body with { gstin = first14 + "?" }) };
        badGstin.Headers.IfMatch.Add(created.Headers.ETag!);
        var refused = await admin.SendAsync(badGstin, Ct);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var update = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/settings/business-profile") { Content = JsonContent.Create(body with { tradeName = "Fresh Orbit Chennai" }) };
        update.Headers.IfMatch.Add(created.Headers.ETag!);
        var updated = await admin.SendAsync(update, Ct);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/settings/business-profile") { Content = JsonContent.Create(body) };
        stale.Headers.IfMatch.Add(created.Headers.ETag!);
        (await admin.SendAsync(stale, Ct)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        // The period now follows the business time zone.
        var period = await InScopeAsync(p => p.GetRequiredService<ISettingsApi>().GetFinancialPeriodAsync(new DateTimeOffset(2027, 3, 31, 20, 0, 0, TimeSpan.Zero), Ct));
        period.Should().Be("2027-28");
    }

    [Fact]
    public async Task Default_order_policy_comes_from_reference_data()
    {
        var policy = await InScopeAsync(p => p.GetRequiredService<ISettingsApi>().GetOrderPolicyAsync(Guid.CreateVersion7(), Ct));

        policy.CityId.Should().BeNull();
        policy.EnabledPaymentMethods.Should().NotBeEmpty();
        policy.MaxDeliveryAttempts.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Settings_need_the_settings_manage_permission()
    {
        using var other = Factory.CreateStaffClient(["orders.view"]);
        (await other.GetAsync("/api/v1/admin/settings/order-policies", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var admin = Factory.CreateStaffClient(["settings.manage"]);
        var list = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/settings/number-series", Ct));
        list.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }
}
