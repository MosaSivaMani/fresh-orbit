using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Identity.Infrastructure.Persistence;

/// <summary>The Identity module's tables, all in the <c>identity</c> schema.</summary>
public sealed class IdentityModuleModel : IModuleModel
{
    public const string SchemaName = "identity";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityModuleModel).Assembly);
    }
}
