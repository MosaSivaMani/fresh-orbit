using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Cart.Infrastructure.Persistence;

/// <summary>The Cart module's tables, all in the <c>cart</c> schema.</summary>
public sealed class CartModuleModel : IModuleModel
{
    public const string SchemaName = "cart";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartModuleModel).Assembly);
    }
}
