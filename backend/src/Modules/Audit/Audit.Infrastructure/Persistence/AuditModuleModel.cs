using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Audit.Infrastructure.Persistence;

/// <summary>The Audit module's tables, all in the <c>audit</c> schema.</summary>
public sealed class AuditModuleModel : IModuleModel
{
    public const string SchemaName = "audit";

    public string Schema => SchemaName;

    /// <summary>AUD-R4: entries are append-only; UPDATE and DELETE are refused by a trigger.</summary>
    public IEnumerable<string> DatabaseObjectsSql =>
    [
        """
        CREATE OR REPLACE FUNCTION audit.refuse_entry_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'audit.entries is append-only';
        END;
        $$;
        """,
        "DROP TRIGGER IF EXISTS entries_append_only ON audit.entries;",
        "CREATE TRIGGER entries_append_only BEFORE UPDATE OR DELETE ON audit.entries FOR EACH ROW EXECUTE FUNCTION audit.refuse_entry_change();",
    ];

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditModuleModel).Assembly);
    }
}
