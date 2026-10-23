using FreshOrbit.SharedKernel.EntityFramework.Platform;

namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>Adds <c>platform.sample_records</c> rows to the shared context; saved with the loader's own save.</summary>
internal sealed class SampleRecordTracker(FreshOrbitDbContext db) : ISampleRecordTracker
{
    public void Track(string tableName, Guid recordId)
    {
        SampleDataPurger.EnsureTableName(tableName);
        db.Add(new SampleRecord(tableName, recordId));
    }
}
