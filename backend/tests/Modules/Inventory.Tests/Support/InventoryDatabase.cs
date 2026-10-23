using FreshOrbit.Inventory.Infrastructure.Seeding;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Testing;

[assembly: AssemblyFixture(typeof(FreshOrbit.Inventory.Tests.Support.InventoryDatabase))]

namespace FreshOrbit.Inventory.Tests.Support;

/// <summary>The test database with every module's schema plus Inventory's reference data (storage-unit ranges).</summary>
public sealed class InventoryDatabase : PostgresFixture, IAsyncLifetime
{
    public static string ReferenceDataPath
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FreshOrbit.slnx")))
                {
                    return Path.Combine(directory.FullName, "data", "reference");
                }
            }

            throw new DirectoryNotFoundException("FreshOrbit.slnx was not found above the test output directory.");
        }
    }

    public new async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await using var db = CreateDbContext();
        await new StorageUnitsReferenceDataLoader(db).LoadAsync(new ReferenceDataContext(ReferenceDataPath), CancellationToken.None);
    }
}
