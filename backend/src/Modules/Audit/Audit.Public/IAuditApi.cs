namespace FreshOrbit.Audit.Public;

/// <summary>What other modules may ask the Audit module.</summary>
public interface IAuditApi
{
    /// <summary>
    /// Adds an append-only audit entry for the current actor. Tracked on the shared DbContext and saved with the
    /// caller's SaveChanges; never saves.
    /// </summary>
    void Record(AuditRecord record);
}

/// <param name="Action">UPPER_SNAKE action, e.g. PRICE_CHANGED.</param>
/// <param name="Before">State before the change (serialized to jsonb), or null.</param>
/// <param name="After">State after the change (serialized to jsonb), or null.</param>
public sealed record AuditRecord(string Action, string EntityType, string EntityId, object? Before, object? After);
