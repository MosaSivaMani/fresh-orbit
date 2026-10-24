using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using FreshOrbit.Cart.Domain.Carts;

namespace FreshOrbit.Cart.Infrastructure.Seeding;

/// <summary>D55: real carts at a sample location or of a sample customer make <c>--purge-sample</c> refuse (no cross-module foreign key would catch them).</summary>
public sealed class CartSamplePurgeParticipant(FreshOrbitDbContext db) : ISamplePurgeParticipant
{
    public async Task<IReadOnlyList<string>> FindBlockersAsync(IReadOnlyDictionary<string, Guid[]> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        var locations = records.IdsOf(SampleTables.FulfillmentLocations);
        var customers = records.IdsOf(SampleTables.Customers);
        if (locations.Count == 0 && customers.Count == 0)
        {
            return [];
        }

        var count = await db.Set<ShoppingCart>().AsNoTracking().CountAsync(x => (x.FulfillmentLocationId != null && locations.Contains(x.FulfillmentLocationId.Value)) || (x.CustomerId != null && customers.Contains(x.CustomerId.Value)), cancellationToken);
        return SampleTables.Blocker(SampleTracking.TableOf<ShoppingCart>(db), count, "carts");
    }
}
