using FreshOrbit.SharedKernel.Web.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Audit.Infrastructure.Recording;

/// <summary>What the admin-write recorder does for a request.</summary>
public enum AdminAuditMode
{
    /// <summary>Not an <c>/api/v1/admin</c> POST, PUT, PATCH or DELETE: nothing is recorded.</summary>
    NotAdminWrite,

    /// <summary>An admin write: every change it saves is recorded.</summary>
    Audited,

    /// <summary>An admin write excluded on purpose (session housekeeping), with a reason.</summary>
    Exempt,
}

/// <param name="Action">UPPER_SNAKE action named for the endpoint, or null to derive one per entity.</param>
public sealed record AdminAuditDecision(AdminAuditMode Mode, string? Action = null, string? ExemptReason = null);

/// <summary>
/// Which requests are audited and under which action name (AUD-R6, docs/product/audit.md). Endpoint metadata
/// (<see cref="AuditEndpointExtensions.WithAuditAction{TBuilder}"/>, <see cref="AuditEndpointExtensions.ExemptFromAudit{TBuilder}"/>)
/// wins; otherwise the central tables below apply, keyed by <c>METHOD route-pattern</c> exactly as mapped.
/// </summary>
public static class AdminAuditPolicy
{
    public const string AdminPrefix = "/api/v1/admin/";

    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete,
    };

    /// <summary>The only admin writes that are not audited, with the reason. Kept tiny on purpose.</summary>
    public static IReadOnlyDictionary<string, string> Exemptions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["POST /api/v1/admin/auth/login"] = "Anonymous caller (no actor yet); failed logins are counted by Identity.",
        ["POST /api/v1/admin/auth/logout"] = "Session housekeeping: revokes the caller's own refresh tokens only.",
    };

    /// <summary>
    /// Meaningful action names for the high-value admin writes of SEC-7. Every other admin write records
    /// <c>&lt;ENTITY&gt;_CREATED|UPDATED|DELETED</c>. Each key must match a mapped endpoint (coverage test).
    /// </summary>
    public static IReadOnlyDictionary<string, string> Actions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Identity: staff, roles, passwords.
        ["POST /api/v1/admin/staff/"] = "STAFF_CREATED",
        ["PUT /api/v1/admin/staff/{staffId:guid}"] = "STAFF_UPDATED",
        ["POST /api/v1/admin/staff/{staffId:guid}/reset-password"] = "STAFF_PASSWORD_RESET",
        ["POST /api/v1/admin/auth/password"] = "STAFF_PASSWORD_CHANGED",
        ["POST /api/v1/admin/roles"] = "ROLE_CREATED",
        ["PUT /api/v1/admin/roles/{roleId:guid}"] = "ROLE_UPDATED",
        ["DELETE /api/v1/admin/roles/{roleId:guid}"] = "ROLE_DELETED",

        // Customers.
        ["POST /api/v1/admin/customers/{customerId:guid}/block"] = "CUSTOMER_BLOCKED",
        ["POST /api/v1/admin/customers/{customerId:guid}/unblock"] = "CUSTOMER_UNBLOCKED",

        // Payments: refunds, cash settlements, reconciliation, webhooks.
        ["POST /api/v1/admin/orders/{orderId:guid}/refunds"] = "REFUND_REQUESTED",
        ["POST /api/v1/admin/refunds/{refundId:guid}/approve"] = "REFUND_APPROVED",
        ["POST /api/v1/admin/refunds/{refundId:guid}/reject"] = "REFUND_REJECTED",
        ["POST /api/v1/admin/refunds/{refundId:guid}/mark-paid"] = "REFUND_MARKED_PAID",
        ["POST /api/v1/admin/cash-settlements"] = "CASH_SETTLEMENT_RECORDED",
        ["POST /api/v1/admin/cash-settlements/{settlementId:guid}/settle"] = "CASH_SETTLEMENT_SETTLED",
        ["POST /api/v1/admin/cash-settlements/{settlementId:guid}/dispute"] = "CASH_SETTLEMENT_DISPUTED",
        ["POST /api/v1/admin/payment-issues/{issueId:guid}/resolve"] = "PAYMENT_ISSUE_RESOLVED",
        ["POST /api/v1/admin/webhook-events/{eventId:guid}/reprocess"] = "WEBHOOK_EVENT_REPROCESSED",

        // Pricing: price, charge and tax schedules.
        ["PUT /api/v1/admin/pricing/locations/{locationId:guid}/variants/{variantId:guid}/price"] = "PRICE_SCHEDULED",
        ["DELETE /api/v1/admin/pricing/locations/{locationId:guid}/variants/{variantId:guid}/price"] = "PRICE_ENDED",
        ["PUT /api/v1/admin/pricing/locations/{locationId:guid}/variants/{variantId:guid}/preparations/{code}/charge"] = "PREPARATION_CHARGE_SCHEDULED",
        ["POST /api/v1/admin/pricing/prices/bulk"] = "PRICES_BULK_IMPORTED",
        ["POST /api/v1/admin/pricing/tax-rules"] = "TAX_RULE_SCHEDULED",

        // Settings.
        ["PUT /api/v1/admin/settings/business-profile"] = "BUSINESS_PROFILE_CHANGED",
        ["PUT /api/v1/admin/settings/number-series/{code}/{period}"] = "NUMBER_SERIES_CHANGED",
        ["PUT /api/v1/admin/settings/order-policies/{key}"] = "ORDER_POLICY_CHANGED",

        // Locations: activation and service.
        ["POST /api/v1/admin/cities/{cityId:guid}/activate"] = "CITY_ACTIVATED",
        ["POST /api/v1/admin/cities/{cityId:guid}/pause"] = "CITY_PAUSED",
        ["POST /api/v1/admin/cities/{cityId:guid}/retire"] = "CITY_RETIRED",

        // Ordering: money-affecting staff decisions.
        ["POST /api/v1/admin/orders/{orderId:guid}/cancel"] = "ORDER_CANCELLED_BY_STAFF",
        ["POST /api/v1/admin/orders/{orderId:guid}/goodwill"] = "ORDER_GOODWILL_GRANTED",
        ["POST /api/v1/admin/complaints/{complaintId:guid}/decide"] = "COMPLAINT_DECIDED",

        // Operations: outbox dead letters (D296); the endpoint records the entry itself (platform rows are not audited).
        ["POST /api/v1/admin/dead-letters/{deadLetterId:guid}/replay"] = "DEAD_LETTER_REPLAYED",
    };

    public static bool IsWriteMethod(string method) => WriteMethods.Contains(method);

    public static string Key(string method, string routePattern) => $"{method.ToUpperInvariant()} {routePattern}";

    /// <summary>Decides for the current request (its method, path and matched endpoint).</summary>
    public static AdminAuditDecision Decide(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var endpoint = http.GetEndpoint();
        var path = http.Request.Path.Value ?? string.Empty;
        return Decide(http.Request.Method, path, (endpoint as RouteEndpoint)?.RoutePattern.RawText, endpoint?.Metadata);
    }

    /// <summary>Decides for a method, a request path (or route pattern) and the endpoint's metadata.</summary>
    public static AdminAuditDecision Decide(string method, string path, string? routePattern, EndpointMetadataCollection? metadata)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        if (!IsWriteMethod(method) || !path.StartsWith(AdminPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new AdminAuditDecision(AdminAuditMode.NotAdminWrite);
        }

        var key = routePattern is null ? null : Key(method, routePattern);
        if (metadata?.GetMetadata<AuditExemptMetadata>() is { } exempt)
        {
            return new AdminAuditDecision(AdminAuditMode.Exempt, ExemptReason: exempt.Reason);
        }

        if (key is not null && Exemptions.TryGetValue(key, out var reason))
        {
            return new AdminAuditDecision(AdminAuditMode.Exempt, ExemptReason: reason);
        }

        var action = metadata?.GetMetadata<AuditActionMetadata>()?.Action
            ?? (key is not null && Actions.TryGetValue(key, out var named) ? named : null);
        return new AdminAuditDecision(AdminAuditMode.Audited, action);
    }
}
