using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>One Postgres database for the Ordering tests, plus a test-only schema for race probes.</summary>
public sealed class OrderingDatabase : PostgresFixture
{
    protected override IEnumerable<IModuleModel> AdditionalModuleModels => [new OrderingTestSupportModel()];
}

/// <summary>
/// Test-only table <c>probes</c> (an aggregate the fake cart touches so a test can force a lost race).
/// </summary>
public sealed class OrderingTestSupportModel : IModuleModel
{
    public const string SchemaName = "ordering_test";

    public string Schema => SchemaName;

    public IEnumerable<string> DatabaseObjectsSql => [];

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Probe>(b =>
        {
            b.ToTable("probes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}

/// <summary>An aggregate with a row version used to simulate a concurrent writer.</summary>
public sealed class Probe : AggregateRoot
{
    public Probe(Guid id)
        : base(id)
    {
    }

    private Probe()
    {
    }

    public int Touches { get; private set; }

    public void Touch() => Touches++;
}
