using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Customers.Infrastructure.Persistence;

/// <summary>The Customers module's tables, all in the <c>customers</c> schema.</summary>
public sealed class CustomersModuleModel : IModuleModel
{
    public const string SchemaName = "customers";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersModuleModel).Assembly);
    }
}
