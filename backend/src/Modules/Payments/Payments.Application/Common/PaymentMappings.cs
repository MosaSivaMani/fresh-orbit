using System.Globalization;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Webhooks;

namespace FreshOrbit.Payments.Application.Common;

public static class PaymentMappings
{
    public static string FormatVersion(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static PaymentSummaryResponse ToSummary(this Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new PaymentSummaryResponse(payment.PaidPaise, payment.RefundedPaise, payment.RefundPendingPaise, payment.RefundablePaise);
    }

    public static PaymentResponse ToResponse(this Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new PaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.FulfillmentLocationId,
            payment.Method,
            payment.Provider,
            payment.Status,
            payment.AmountPaise,
            payment.Currency,
            payment.ProviderOrderId,
            payment.ProviderPaymentId,
            payment.ExpiresAt,
            payment.CapturedAt,
            payment.CollectedPaise,
            payment.CollectedAt,
            payment.CollectedByRiderId,
            payment.CollectionMode,
            payment.CollectionReference,
            payment.CashSettlementId,
            payment.OrderDeliveredAt,
            payment.IsExcess,
            payment.FailureCode,
            payment.FailureMessage,
            payment.ToSummary(),
            FormatVersion(payment.Version),
            payment.CreatedAt);
    }

    public static RefundResponse ToResponse(this Refund refund, Payment payment, bool maskUpi = false)
    {
        ArgumentNullException.ThrowIfNull(refund);
        ArgumentNullException.ThrowIfNull(payment);
        return new RefundResponse(
            refund.Id,
            refund.OrderId,
            refund.PaymentId,
            refund.FulfillmentLocationId,
            refund.AmountPaise,
            refund.ReasonCode,
            refund.Method,
            refund.Status,
            refund.RequestedBy,
            refund.RequestedAt,
            refund.ApprovedBy,
            refund.ApprovedAt,
            refund.ComplaintId,
            maskUpi ? UpiMask.Mask(refund.PayeeUpiId) : refund.PayeeUpiId,
            refund.Notes,
            refund.FailureReason,
            refund.ProviderRefundId,
            refund.ManualReference,
            refund.SucceededAt,
            refund.AttemptCount,
            FormatVersion(payment.Version),
            payment.ToSummary());
    }

    public static CustomerPaymentResponse ToCustomerResponse(this Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new CustomerPaymentResponse(
            payment.Id,
            payment.Method,
            payment.Provider,
            payment.Status,
            payment.AmountPaise,
            payment.ProviderOrderId,
            payment.ExpiresAt,
            payment.CapturedAt,
            payment.CollectedAt,
            payment.FailureCode);
    }

    public static CustomerRefundResponse ToCustomerResponse(this Refund refund)
    {
        ArgumentNullException.ThrowIfNull(refund);
        return new CustomerRefundResponse(refund.Id, refund.AmountPaise, refund.ReasonCode, refund.Method, refund.Status, refund.RequestedAt, refund.SucceededAt);
    }

    public static WebhookEventResponse ToResponse(this WebhookEvent webhookEvent)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        return new WebhookEventResponse(
            webhookEvent.Id,
            webhookEvent.Provider,
            webhookEvent.ProviderEventId,
            webhookEvent.EventType,
            webhookEvent.SignatureValid,
            webhookEvent.Status,
            webhookEvent.ReceivedAt,
            webhookEvent.ProcessedAt,
            webhookEvent.Error,
            webhookEvent.PayloadSha256,
            webhookEvent.PayloadTruncated);
    }

    public static CashSettlementResponse ToResponse(this CashSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return new CashSettlementResponse(
            settlement.Id,
            settlement.RiderId,
            settlement.FulfillmentLocationId,
            settlement.SettlementDate,
            settlement.ExpectedPaise,
            settlement.ReceivedPaise,
            settlement.DifferencePaise,
            settlement.Status,
            settlement.CollectionsCount,
            settlement.RecordedBy,
            settlement.SettledBy,
            settlement.SettledAt,
            settlement.Note,
            settlement.DisputeReason,
            FormatVersion(settlement.Version));
    }

    public static PaymentIssueResponse ToResponse(this ReconciliationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new PaymentIssueResponse(
            issue.Id,
            issue.Kind,
            issue.SubjectType,
            issue.SubjectId,
            issue.OrderId,
            issue.FulfillmentLocationId,
            issue.Details,
            issue.Status,
            issue.DetectedAt,
            issue.ResolvedAt,
            issue.ResolvedBy,
            issue.Note,
            FormatVersion(issue.Version));
    }
}
