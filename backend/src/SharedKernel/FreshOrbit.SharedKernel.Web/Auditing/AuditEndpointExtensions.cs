using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Builder;

namespace FreshOrbit.SharedKernel.Web.Auditing;

/// <summary>Endpoint metadata: the UPPER_SNAKE audit action of an admin write (docs/product/audit.md §2).</summary>
public sealed record AuditActionMetadata(string Action);

/// <summary>Endpoint metadata: this admin write is not audited, with the reason (docs/product/audit.md §3).</summary>
public sealed record AuditExemptMetadata(string Reason);

public static class AuditEndpointExtensions
{
    /// <summary>Names the audit action recorded for every change this admin write saves (e.g. <c>REFUND_APPROVED</c>).</summary>
    public static TBuilder WithAuditAction<TBuilder>(this TBuilder builder, string action)
        where TBuilder : IEndpointConventionBuilder
    {
        ErrorCodes.EnsureValid(action);
        return builder.WithMetadata(new AuditActionMetadata(action));
    }

    /// <summary>
    /// Excludes this admin write from the audit trail. Only for session housekeeping such as login or logout;
    /// the endpoint coverage test lists every exemption.
    /// </summary>
    public static TBuilder ExemptFromAudit<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return builder.WithMetadata(new AuditExemptMetadata(reason));
    }
}
