using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Public;
using FreshOrbit.SharedKernel.Codes;
using DomainStatus = FreshOrbit.Payments.Domain.Payments.PaymentStatus;
using PublicStatus = FreshOrbit.Payments.Public.PaymentStatus;

namespace FreshOrbit.Payments.Application.Payments;

/// <summary>Builds the <c>Payments.Public</c> summaries from an order's payment attempts.</summary>
public static class PaymentSummaries
{
    public static OrderPaymentSummary Build(Guid orderId, IEnumerable<Payment> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        var list = attempts.Where(p => p.OrderId == orderId).ToList();
        var latest = list.OrderByDescending(p => p.IsPaid).ThenByDescending(p => p.Id).FirstOrDefault();
        var counted = list.Where(p => p.IsPaid && !p.IsExcess).ToList();
        var paid = counted.Sum(p => p.PaidPaise);
        var refunded = list.Sum(p => p.RefundedPaise);
        var pending = list.Sum(p => p.RefundPendingPaise);
        var refundable = counted.Sum(p => p.RefundablePaise);
        var next = latest switch
        {
            null => PaymentNextAction.None,
            { IsPaid: true } => PaymentNextAction.None,
            { IsDeliveryCollected: true, Status: DomainStatus.Pending } => PaymentNextAction.PayOnDelivery,
            { Method: PaymentMethod.Online } when counted.Count == 0 => PaymentNextAction.InitiatePayment,
            _ => PaymentNextAction.None,
        };
        return new OrderPaymentSummary(orderId, latest?.Method.ToCode(), ToPublic(latest?.Status), paid, refunded, pending, refundable, next);
    }

    public static OrderPaymentStatus BuildStatus(Guid orderId, IEnumerable<Payment> attempts)
    {
        var summary = Build(orderId, attempts);
        return new OrderPaymentStatus(orderId, summary.LatestStatus, summary.PaidPaise, summary.RefundedPaise);
    }

    private static PublicStatus ToPublic(DomainStatus? status) => status switch
    {
        null => PublicStatus.None,
        DomainStatus.Pending => PublicStatus.Pending,
        DomainStatus.Authorized => PublicStatus.Authorized,
        DomainStatus.Captured => PublicStatus.Captured,
        DomainStatus.Collected => PublicStatus.Collected,
        DomainStatus.Failed => PublicStatus.Failed,
        _ => PublicStatus.Cancelled,
    };
}
