using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Reporting.Infrastructure.Persistence;

/// <summary>The Reporting module's tables, all in the <c>reporting</c> schema.</summary>
public sealed class ReportingModuleModel : IModuleModel
{
    public const string SchemaName = "reporting";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingModuleModel).Assembly);
    }
}
