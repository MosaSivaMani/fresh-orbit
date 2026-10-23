using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Promotions.Infrastructure.Persistence;

/// <summary>The Promotions module's tables, all in the <c>promotions</c> schema.</summary>
public sealed class PromotionsModuleModel : IModuleModel
{
    public const string SchemaName = "promotions";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionsModuleModel).Assembly);
    }
}
