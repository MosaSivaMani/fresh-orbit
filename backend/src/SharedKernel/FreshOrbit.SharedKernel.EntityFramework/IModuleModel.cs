using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.EntityFramework;

/// <summary>
/// A module's part of the one <see cref="FreshOrbitDbContext"/> model. Register it as a singleton
/// <c>IModuleModel</c>; the context calls <see cref="Configure"/> on every registered model.
/// <list type="bullet">
/// <item>Every entity type added by <see cref="Configure"/> without an explicit schema is placed in <see cref="Schema"/>.</item>
/// <item>Name tables explicitly in plural snake_case (<c>builder.ToTable("stock_items")</c>); columns are snake_cased by convention.</item>
/// <item>Audit columns, the <c>xmin</c> row version of aggregate roots, UPPER_SNAKE enum codes and UTC timestamps are applied by convention.</item>
/// </list>
/// </summary>
public interface IModuleModel
{
    /// <summary>Lowercase Postgres schema owned by the module (e.g. <c>catalog</c>).</summary>
    string Schema { get; }

    void Configure(ModelBuilder modelBuilder);
}
