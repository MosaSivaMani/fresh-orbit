using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using FreshOrbit.Catalog.Tests.Support;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Public;
using FreshOrbit.Pricing.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

[assembly: AssemblyFixture(typeof(PricingDatabase))]

namespace FreshOrbit.Pricing.Tests.Integration;

public sealed class PricingDatabase : StorefrontDatabase;

/// <summary>pricing.md §6 integration scenarios.</summary>
public sealed class PricingTests(PricingDatabase database) : StorefrontTestBase(database)
{
    private static string PriceUrl(Guid locationId, Guid variantId) => $"/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price";

    [Fact]
    public async Task Price_schedule_needs_if_none_match_first_then_the_current_version()
    {
        var served = await CreateServedLocationAsync();
        var product = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Seer Fish");
        var url = PriceUrl(served.LocationId, product.VariantId);

        var first = await OkAsync(HttpMethod.Put, url, new { unitPricePaise = 129900, compareAtPaise = 149900 }, ifNoneMatchAny: true);
        first.GetProperty("current").GetProperty("unitPricePaise").GetInt64().Should().Be(129900);
        var version = first.GetProperty("version").GetString()!;

        var missing = await SendAsync(Admin, HttpMethod.Put, url, new { unitPricePaise = 124900 });
        missing.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        var again = await SendAsync(Admin, HttpMethod.Put, url, new { unitPricePaise = 124900 }, ifNoneMatchAny: true);
        again.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        var stale = await SendAsync(Admin, HttpMethod.Put, url, new { unitPricePaise = 124900 }, "1");
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var next = Factory.Clock.UtcNow.AddHours(2);
        var scheduled = await OkAsync(HttpMethod.Put, url, new { unitPricePaise = 124900, compareAtPaise = 149900, validFrom = next }, version);
        scheduled.GetProperty("current").GetProperty("validTo").GetDateTimeOffset().Should().Be(next);
        scheduled.GetProperty("next").GetProperty("unitPricePaise").GetInt64().Should().Be(124900);

        var unchanged = await OkAsync(HttpMethod.Put, url, new { unitPricePaise = 124900, compareAtPaise = 149900, validFrom = next }, scheduled.GetProperty("version").GetString());
        (await SendAsync(Admin, HttpMethod.Put, url, new { unitPricePaise = 99900, validFrom = Factory.Clock.UtcNow.AddHours(1) }, scheduled.GetProperty("version").GetString()))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        unchanged.GetProperty("history").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Overlapping_periods_are_refused_by_the_database()
    {
        var location = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        await ExecuteSqlAsync($"INSERT INTO pricing.variant_prices (id, fulfillment_location_id, variant_id, unit_price_paise, valid_from, valid_to, created_at) VALUES ('{Guid.CreateVersion7()}', '{location}', '{variant}', 1000, '2026-01-01Z', NULL, now())");

        var overlap = () => ExecuteSqlAsync($"INSERT INTO pricing.variant_prices (id, fulfillment_location_id, variant_id, unit_price_paise, valid_from, valid_to, created_at) VALUES ('{Guid.CreateVersion7()}', '{location}', '{variant}', 2000, '2026-06-01Z', NULL, now())");

        (await overlap.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23P01");
    }

    [Fact]
    public async Task A_price_needs_a_tax_rule_for_the_products_category()
    {
        var served = await CreateServedLocationAsync();
        var product = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Dry Fish", taxCategory: UntaxedCategory);

        var response = await SendAsync(Admin, HttpMethod.Put, PriceUrl(served.LocationId, product.VariantId), new { unitPricePaise = 59900 }, ifNoneMatchAny: true);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(response)).Should().Be("TAX_RULE_MISSING");
    }

    [Fact]
    public async Task Bulk_upload_is_all_or_nothing_and_dry_run_writes_nothing()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var a = await CreatePublishedProductAsync(category, "Seer Fish");
        var b = await CreatePublishedProductAsync(category, "Tiger Prawns");
        object[] rows =
        [
            new { kind = "PRICE", locationCode = served.LocationCode, sku = a.Sku, unitPricePaise = 129900 },
            new { kind = "PRICE", locationCode = served.LocationCode, sku = b.Sku, unitPricePaise = 0 },
            new { kind = "PREP_CHARGE", locationCode = served.LocationCode, sku = a.Sku, preparationCode = "CURRY_CUT", chargePaise = 4000, chargeBasis = "PER_KG" },
        ];

        var invalid = await PostBulkAsync(new { rows }, dryRun: false);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errors = (await ReadJsonAsync(invalid)).GetProperty("errors");
        errors.TryGetProperty("rows[1].unitPricePaise", out _).Should().BeTrue(errors.ToString());
        (await CountPricesAsync(served.LocationId)).Should().Be(0);

        rows[1] = new { kind = "PRICE", locationCode = served.LocationCode, sku = b.Sku, unitPricePaise = 89900 };
        var dryRun = await PostBulkAsync(new { rows }, dryRun: true);
        dryRun.StatusCode.Should().Be(HttpStatusCode.OK);
        var diff = await ReadJsonAsync(dryRun);
        diff.GetProperty("changedCount").GetInt32().Should().Be(3);
        diff.GetProperty("uploadId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        (await CountPricesAsync(served.LocationId)).Should().Be(0);

        var applied = await PostBulkAsync(new { rows }, dryRun: false);
        applied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountPricesAsync(served.LocationId)).Should().Be(3);

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var csv = $"kind,location_code,sku,unit_price_paise\nPRICE,{served.LocationCode},{a.Sku},134900\nPRICE,{served.LocationCode},{b.Sku},89900\n";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/pricing/prices/bulk") { Content = new StringContent(csv, Encoding.UTF8, "text/csv") };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var csvResponse = await Admin.SendAsync(request, Ct);
        csvResponse.StatusCode.Should().Be(HttpStatusCode.OK, await csvResponse.Content.ReadAsStringAsync(Ct));
        var csvResult = await ReadJsonAsync(csvResponse);
        csvResult.GetProperty("changedCount").GetInt32().Should().Be(1);
        csvResult.GetProperty("unchangedCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task City_readiness_needs_a_charge_for_the_default_preparation()
    {
        var served = await CreateServedLocationAsync();
        var product = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Seer Fish");
        await OkAsync(HttpMethod.Put, PriceUrl(served.LocationId, product.VariantId), new { unitPricePaise = 129900 }, ifNoneMatchAny: true);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var check = scope.ServiceProvider.GetServices<ICityActivationCheck>().Single(c => c.GetType().Name == "PricingCityActivationCheck");
            var missing = await check.GetMissingDetailsAsync(served.CityId, [served.LocationId], Ct);
            missing.Should().ContainSingle(m => m.Code == "PRICE_MISSING" && m.FulfillmentLocationId == served.LocationId);
        }

        await OkAsync(HttpMethod.Put, $"/api/v1/admin/pricing/locations/{served.LocationId}/variants/{product.VariantId}/preparations/CURRY_CUT/charge", new { chargePaise = 0, chargeBasis = "FLAT" }, ifNoneMatchAny: true);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var check = scope.ServiceProvider.GetServices<ICityActivationCheck>().Single(c => c.GetType().Name == "PricingCityActivationCheck");
            (await check.GetMissingDetailsAsync(served.CityId, [served.LocationId], Ct)).Should().BeEmpty();
            (await check.GetMissingItemsAsync(served.CityId, Ct)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Offers_and_line_prices_report_what_is_missing_per_variant()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var priced = await CreatePublishedProductAsync(category, "Seer Fish");
        var unpriced = await CreatePublishedProductAsync(category, "Pomfret");
        await OkAsync(HttpMethod.Put, PriceUrl(served.LocationId, priced.VariantId), new { unitPricePaise = 129900 }, ifNoneMatchAny: true);
        await OkAsync(HttpMethod.Put, $"/api/v1/admin/pricing/locations/{served.LocationId}/variants/{priced.VariantId}/preparations/CURRY_CUT/charge", new { chargePaise = 4000, chargeBasis = "PER_KG" }, ifNoneMatchAny: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var api = scope.ServiceProvider.GetRequiredService<IPricingApi>();
        var at = Factory.Clock.UtcNow;
        var offers = await api.GetOffersAsync(served.LocationId, [priced.VariantId, unpriced.VariantId, Guid.CreateVersion7()], at, Ct);

        offers.Should().HaveCount(2);
        var offer = offers.Single(o => o.VariantId == priced.VariantId);
        offer.IssueCode.Should().BeNull();
        offer.TaxRateBp.Should().Be(500);
        offer.PriceUnit.Should().Be("KG");
        offer.Preparations.Should().ContainSingle(p => p.PreparationCode == "WHOLE" && p.IssueCode == "PREPARATION_CHARGE_NOT_AVAILABLE");
        offer.Preparations.Should().ContainSingle(p => p.PreparationCode == "CURRY_CUT" && p.ChargePaise == 4000 && p.ChargeBasis == Public.ChargeBasis.PerKg);
        offers.Single(o => o.VariantId == unpriced.VariantId).IssueCode.Should().Be("PRICE_NOT_AVAILABLE");

        var curryCut = offer.Preparations.Single(p => p.PreparationCode == "CURRY_CUT").PreparationMethodId;
        var whole = offer.Preparations.Single(p => p.PreparationCode == "WHOLE").PreparationMethodId;
        var lines = await api.GetLinePricesAsync(served.LocationId, [new(priced.VariantId, curryCut), new(priced.VariantId, whole), new(priced.VariantId, null), new(unpriced.VariantId, null)], at, Ct);
        lines.Should().HaveCount(2);
        lines.Should().ContainSingle(l => l.PreparationMethodId == curryCut && l.PreparationChargePaise == 4000 && l.UnitPricePaise == 129900 && l.TaxRateBp == 500);
        lines.Should().ContainSingle(l => l.PreparationMethodId == null && l.PreparationChargePaise == 0 && l.ChargeBasis == null);
        (await api.GetTaxRatesAsync([TaxCategory, UntaxedCategory], at, Ct)).Should().ContainKey(TaxCategory).And.NotContainKey(UntaxedCategory);
    }

    [Fact]
    public async Task Pricing_needs_pricing_manage_and_access_to_the_location()
    {
        var served = await CreateServedLocationAsync();
        var product = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Seer Fish");
        using var otherLocation = Factory.CreateStaffClient(["pricing.manage"], [Guid.CreateVersion7()]);
        using var noPermission = Factory.CreateStaffClient(["catalog.manage"], allLocations: true);

        var denied = await SendAsync(otherLocation, HttpMethod.Put, PriceUrl(served.LocationId, product.VariantId), new { unitPricePaise = 129900 }, ifNoneMatchAny: true);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");

        (await noPermission.GetAsync($"/api/v1/admin/pricing/prices?fulfillmentLocationId={served.LocationId}", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var list = await OkAsync(HttpMethod.Get, $"/api/v1/admin/pricing/prices?fulfillmentLocationId={served.LocationId}&missingOnly=true&q={product.Sku}");
        list.GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("variantId").GetGuid() == product.VariantId && i.GetProperty("missing").GetBoolean());
    }

    [Fact]
    public async Task Tax_rule_writes_need_tax_manage_so_store_managers_are_refused_and_finance_succeeds()
    {
        using var storeManager = Factory.CreateStaffClient(SeededPermissionsOf("STORE_MANAGER"), [Guid.CreateVersion7()]);
        using var finance = Factory.CreateStaffClient(SeededPermissionsOf("FINANCE"), allLocations: true);
        var rule = new { taxCategoryCode = TaxCategory, rateBp = 500 };

        var refused = await SendAsync(storeManager, HttpMethod.Post, "/api/v1/admin/pricing/tax-rules", rule);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await storeManager.GetAsync("/api/v1/admin/pricing/tax-rules", Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "pricing.manage may still read tax rules");

        var scheduled = await SendAsync(finance, HttpMethod.Post, "/api/v1/admin/pricing/tax-rules", rule);
        scheduled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(scheduled)).GetProperty("taxCategoryCode").GetString().Should().Be(TaxCategory);
        (await finance.GetAsync("/api/v1/admin/pricing/tax-rules", Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "tax.manage may read tax rules");
    }

    /// <summary>A system role's permissions as seeded by <c>backend/data/reference/identity/roles.json</c>.</summary>
    private static string[] SeededPermissionsOf(string roleCode)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "data", "reference", "identity", "roles.json");
            if (File.Exists(path))
            {
                using var roles = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                return [.. roles.RootElement.GetProperty("roles").EnumerateArray()
                    .Single(r => r.GetProperty("code").GetString() == roleCode)
                    .GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!)];
            }
        }

        throw new FileNotFoundException("backend/data/reference/identity/roles.json was not found above the test output directory.");
    }

    private async Task<HttpResponseMessage> PostBulkAsync(object body, bool dryRun)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/pricing/prices/bulk?dryRun={(dryRun ? "true" : "false")}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Admin.SendAsync(request, Ct);
    }

    private async Task<int> CountPricesAsync(Guid locationId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<VariantPrice>().CountAsync(p => p.FulfillmentLocationId == locationId, Ct)
            + await db.Set<PreparationCharge>().CountAsync(p => p.FulfillmentLocationId == locationId, Ct);
    }
}
