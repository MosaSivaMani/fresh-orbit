using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Delivery.Infrastructure.Persistence;

/// <summary>The Delivery module's tables, all in the <c>delivery</c> schema.</summary>
public sealed class DeliveryModuleModel : IModuleModel
{
    public const string SchemaName = "delivery";

    /// <summary>Name of the EXCLUDE constraint backing DEL-R2 (violation SQLSTATE 23P01 → 409 SLOT_TEMPLATE_OVERLAP).</summary>
    public const string TemplateOverlapConstraint = "ex_slot_templates_no_overlap";

    public string Schema => SchemaName;

    /// <summary>A5 / D12: active templates of one location and day never overlap (<c>[start, end)</c>).</summary>
    public IEnumerable<string> DatabaseObjectsSql =>
    [
        $"""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{TemplateOverlapConstraint}') THEN
                ALTER TABLE delivery.slot_templates ADD CONSTRAINT {TemplateOverlapConstraint}
                    EXCLUDE USING gist (
                        fulfillment_location_id WITH =,
                        day_of_week WITH =,
                        tsrange(DATE '2000-01-01' + start_time, DATE '2000-01-01' + end_time) WITH &&)
                    WHERE (is_active);
            END IF;
        END
        $$;
        """,
    ];

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryModuleModel).Assembly);
    }
}
