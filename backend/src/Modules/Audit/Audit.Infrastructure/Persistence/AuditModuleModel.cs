using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Audit.Infrastructure.Persistence;

/// <summary>The Audit module's tables, all in the <c>audit</c> schema.</summary>
public sealed class AuditModuleModel : IModuleModel
{
    public const string SchemaName = "audit";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditModuleModel).Assembly);
    }
}
