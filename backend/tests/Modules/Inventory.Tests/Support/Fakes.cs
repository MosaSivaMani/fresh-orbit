using System.Collections.Concurrent;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;

namespace FreshOrbit.Inventory.Tests.Support;

/// <summary>Catalog.Public substitute: variants added by tests.</summary>
public sealed class FakeCatalogApi : ICatalogApi
{
    private readonly ConcurrentDictionary<Guid, VariantInfo> _variants = new();

    public Guid AddVariant(StockUnit stockUnit = StockUnit.Gram, bool isActive = true)
    {
        var id = Guid.CreateVersion7();
        _variants[id] = new VariantInfo(
            id, Guid.CreateVersion7(), Guid.CreateVersion7(), "Seer fish", "seer-fish", "Curry cut", $"SKU-{id:N}", null, "0302", "FRESH_FISH",
            stockUnit == StockUnit.Gram ? SellUnit.Kg : SellUnit.Piece, stockUnit, null, null, 250, 5000, 250, stockUnit == StockUnit.Gram, isActive, []);
        return id;
    }

    public Task<IReadOnlyList<VariantInfo>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<VariantInfo>>([.. variantIds.Where(_variants.ContainsKey).Select(id => _variants[id])]);
}

/// <summary>Locations.Public substitute: locations added by tests (default time zone Asia/Kolkata).</summary>
public sealed class FakeLocationsApi : ILocationsApi
{
    private readonly ConcurrentDictionary<Guid, FulfillmentLocationSummary> _locations = new();

    public Guid AddLocation(Guid? cityId = null, string timeZone = "Asia/Kolkata")
    {
        var id = Guid.CreateVersion7();
        _locations[id] = new FulfillmentLocationSummary(id, cityId ?? Guid.CreateVersion7(), $"LOC-{id.ToString("N")[..6]}", "Test store", FulfillmentLocationStatus.Active, timeZone);
        return id;
    }

    public Task<Serviceability> ResolvePincodeAsync(string pincode, CancellationToken cancellationToken = default)
        => Task.FromResult(new Serviceability(false, pincode, null, null, null, null, null, null, null, null));

    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FulfillmentLocationSummary>>([.. fulfillmentLocationIds.Where(_locations.ContainsKey).Select(id => _locations[id])]);
}
