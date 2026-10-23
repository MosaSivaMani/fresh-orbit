using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Pricing.Infrastructure.Persistence;

/// <summary>The Pricing module's tables, all in the <c>pricing</c> schema.</summary>
public sealed class PricingModuleModel : IModuleModel
{
    public const string SchemaName = "pricing";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingModuleModel).Assembly);
    }
}
