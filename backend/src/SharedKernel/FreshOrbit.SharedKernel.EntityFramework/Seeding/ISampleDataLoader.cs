namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>
/// Loads demo data from <c>backend/data/sample</c> (only with <c>--sample</c>, refused in Production).
/// Every inserted row must be tracked through <see cref="SampleDataContext.Tracker"/> so <c>--purge-sample</c>
/// can remove it. Loaders run in ascending <see cref="Order"/>; each saves its own changes.
/// </summary>
public interface ISampleDataLoader
{
    int Order { get; }

    Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken);
}

public sealed record SampleDataContext(string DirectoryPath, ISampleRecordTracker Tracker);

/// <summary>Records an inserted sample row (<c>schema.table</c> + its <c>id</c>) in <c>platform.sample_records</c>.</summary>
public interface ISampleRecordTracker
{
    void Track(string tableName, Guid recordId);
}
