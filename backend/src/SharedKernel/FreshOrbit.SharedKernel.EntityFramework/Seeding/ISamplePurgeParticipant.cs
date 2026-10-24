namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>
/// A module's part in <c>--purge-sample</c> (decision D55). Every participant is asked for blockers first; when any
/// reports one, nothing is deleted. Then, inside the purge transaction, each participant may remove rows it generated
/// from sample rows (e.g. Delivery's unbooked slots of sample templates) before the tracked rows are deleted.
/// </summary>
public interface ISamplePurgeParticipant
{
    /// <summary>
    /// One sentence per kind of real row that still points at sample rows (e.g. "ordering.orders: 3 orders use a
    /// sample location or customer."); empty when the purge may go ahead. <paramref name="records"/> maps
    /// <c>schema.table</c> to the tracked sample ids.
    /// </summary>
    Task<IReadOnlyList<string>> FindBlockersAsync(IReadOnlyDictionary<string, Guid[]> records, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>Deletes rows generated from sample rows that are not tracked themselves; runs in the purge transaction.</summary>
    Task PrepareAsync(IReadOnlyDictionary<string, Guid[]> records, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>The purge was refused; <see cref="Blockers"/> names what still uses the sample rows. Nothing was deleted.</summary>
public sealed class SamplePurgeRefusedException(IReadOnlyList<string> blockers)
    : InvalidOperationException("Sample data cannot be purged: " + string.Join(" ", blockers) + " Drop the development database to start clean (D55).")
{
    public IReadOnlyList<string> Blockers { get; } = blockers;
}

/// <summary>Tracked tables other modules check for references in <see cref="ISamplePurgeParticipant.FindBlockersAsync"/>.</summary>
public static class SampleTables
{
    public const string FulfillmentLocations = "locations.fulfillment_locations";

    public const string Customers = "customers.customers";

    /// <summary>A purge participant's blocker sentence for rows of <paramref name="table"/> that use sample locations or customers.</summary>
    public static IReadOnlyList<string> Blocker(string table, int count, string what)
        => count == 0 ? [] : [$"{table}: {count} {what} use a sample location or customer."];
}
