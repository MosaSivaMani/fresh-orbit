using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Persistence;

/// <summary>The Payments module's tables, all in the <c>payments</c> schema.</summary>
public sealed class PaymentsModuleModel : IModuleModel
{
    public const string SchemaName = "payments";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsModuleModel).Assembly);
    }
}
