using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using FreshOrbit.Payments.Domain.Payments;

namespace FreshOrbit.Payments.Infrastructure.Seeding;

/// <summary>D55: real payments at a sample location or of a sample customer make <c>--purge-sample</c> refuse (no cross-module foreign key would catch them).</summary>
public sealed class PaymentsSamplePurgeParticipant(FreshOrbitDbContext db) : ISamplePurgeParticipant
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

        var count = await db.Set<Payment>().AsNoTracking().CountAsync(x => locations.Contains(x.FulfillmentLocationId) || customers.Contains(x.CustomerId), cancellationToken);
        return SampleTables.Blocker(SampleTracking.TableOf<Payment>(db), count, "payments");
    }
}
