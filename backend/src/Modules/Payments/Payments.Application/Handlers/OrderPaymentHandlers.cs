using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Handlers;

// Plain inputs of the consumed events (docs/product/payments.md §4, ordering.md §4, delivery.md). The manager maps the
// final Ordering / Delivery event classes onto these at merge; codes are the stored UPPER_SNAKE strings.

/// <param name="PaymentMethod">COD, UPI_ON_DELIVERY or ONLINE.</param>
public sealed record OrderPlacedInput(Guid OrderId, Guid CustomerId, Guid FulfillmentLocationId, string PaymentMethod, long AmountDuePaise);

/// <param name="RefundDueByReason">Prepaid refund due per reason code (WEIGHT_SHORTFALL, SHORT_SUPPLY).</param>
public sealed record OrderPackedInput(Guid OrderId, string PaymentMethod, long AmountDuePaise, long RefundDuePaise, IReadOnlyDictionary<string, long> RefundDueByReason);

/// <param name="RequestedBy">Staff user who made the goodwill adjustment (the refund requester).</param>
public sealed record OrderAdjustedInput(Guid OrderId, Guid AdjustmentId, long AmountDuePaise, long RefundDuePaise, Guid? RequestedBy);

/// <param name="CashCollectedPaise">Cash entered by the rider at deliver, if any.</param>
/// <param name="CashMode">CASH or UPI.</param>
public sealed record ShipmentDeliveredInput(Guid OrderId, Guid ShipmentId, Guid? RiderId, DateTimeOffset DeliveredAt, long? CashCollectedPaise, string? CashMode, string? UpiReference);

/// <param name="RefundMethod">ORIGINAL, UPI, BANK_TRANSFER or CASH; null = ORIGINAL for online payments.</param>
public sealed record ComplaintDecidedInput(Guid OrderId, Guid ComplaintId, string Resolution, long? RefundAmountPaise, string? RefundMethod, Guid DecidedBy);

/// <summary>
/// Handler logic for other modules' events (PAY-H1..H8). Methods change tracked aggregates and never save: the outbox
/// dispatcher saves the handler's changes with its inbox row. Each has a natural guard so a re-delivery has no effect.
/// </summary>
public sealed class OrderPaymentHandlers(IPaymentRepository payments, IPaymentProviderCatalog providers, IssueRecorder issues, IClock clock)
{
    /// <summary>PAY-H1 / PAY-R3: COD or UPI on delivery → one PENDING payment of the amount due; ONLINE → nothing.</summary>
    public async Task OnOrderPlacedAsync(OrderPlacedInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!EnumCodes.TryToEnum<PaymentMethod>(input.PaymentMethod, out var method) || method == PaymentMethod.Online)
        {
            return;
        }

        var existing = await payments.ListByOrderAsync(input.OrderId, cancellationToken);
        if (existing.Any(p => p.IsDeliveryCollected))
        {
            return;
        }

        payments.Add(Payment.CreateForDelivery(input.OrderId, input.CustomerId, input.FulfillmentLocationId, method, input.AmountDuePaise));
    }

    /// <summary>PAY-H2: COD amount due follows the packed total (PAY-R8); ONLINE → one system refund per reason (PAY-R15).</summary>
    public async Task OnOrderPackedAsync(OrderPackedInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var attempts = await payments.ListByOrderAsync(input.OrderId, cancellationToken);
        await FollowAmountDueAsync(attempts, input.AmountDuePaise, cancellationToken);
        var paid = PaidOnline(attempts);
        if (paid is null)
        {
            return;
        }

        foreach (var (reasonCode, amount) in input.RefundDueByReason.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            if (amount > 0 && EnumCodes.TryToEnum<RefundReason>(reasonCode, out var reason))
            {
                await CreateSystemRefundAsync(paid, reason, amount, $"OrderPacked:{input.OrderId}:{reasonCode}", requiresApproval: false, null, cancellationToken);
            }
        }
    }

    /// <summary>PAY-H3: amount due follows; ONLINE refund due → GOODWILL refund REQUESTED by the staff who adjusted.</summary>
    public async Task OnOrderAdjustedAsync(OrderAdjustedInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var attempts = await payments.ListByOrderAsync(input.OrderId, cancellationToken);
        await FollowAmountDueAsync(attempts, input.AmountDuePaise, cancellationToken);
        if (input.RefundDuePaise > 0 && PaidOnline(attempts) is { } paid)
        {
            await CreateSystemRefundAsync(paid, RefundReason.Goodwill, input.RefundDuePaise, $"OrderAdjusted:{input.AdjustmentId}", requiresApproval: true, input.RequestedBy, cancellationToken);
        }
    }

    /// <summary>PAY-H4: open attempts → CANCELLED (PAY-R6); paid ONLINE → CANCELLATION refund of everything refundable.</summary>
    public async Task OnOrderCancelledAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
        CancelOpenAttempts(attempts);
        if (PaidOnline(attempts) is { } paid)
        {
            await CreateSystemRefundAsync(paid, RefundReason.Cancellation, paid.RefundablePaise, $"OrderCancelled:{orderId}", requiresApproval: false, null, cancellationToken);
        }
    }

    /// <summary>PAY-H5: order PAYMENT_FAILED → open attempts CANCELLED.</summary>
    public async Task OnOrderPaymentFailedAsync(Guid orderId, CancellationToken cancellationToken)
        => CancelOpenAttempts(await payments.ListByOrderAsync(orderId, cancellationToken));

    /// <summary>PAY-H5: order RETURNED → open attempts CANCELLED; paid ONLINE → RETURN refund REQUESTED (D19, approval needed).</summary>
    public async Task OnOrderReturnedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
        CancelOpenAttempts(attempts);
        if (PaidOnline(attempts) is { } paid)
        {
            await CreateSystemRefundAsync(paid, RefundReason.Return, paid.RefundablePaise, $"OrderReturned:{orderId}", requiresApproval: true, null, cancellationToken);
        }
    }

    /// <summary>
    /// PAY-H6: records <c>order_delivered_at</c> and the cash given at deliver. A different amount is recorded as given
    /// and an issue COLLECTION_DIFFERENCE is opened (PAY-R4 deliver path).
    /// </summary>
    public async Task OnShipmentDeliveredAsync(ShipmentDeliveredInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var attempts = await payments.ListByOrderAsync(input.OrderId, cancellationToken);
        foreach (var payment in attempts.Where(p => p.IsDeliveryCollected || p.IsPaid))
        {
            payment.MarkOrderDelivered(input.DeliveredAt);
        }

        var delivery = attempts.FirstOrDefault(p => p.IsDeliveryCollected && p.Status is PaymentStatus.Pending or PaymentStatus.Collected);
        if (delivery is null || input.CashCollectedPaise is not { } cash || cash <= 0)
        {
            return;
        }

        if (delivery.Status == PaymentStatus.Pending)
        {
            var mode = EnumCodes.TryToEnum<CollectionMode>(input.CashMode, out var parsed) ? parsed : CollectionMode.Cash;
            var reference = mode == CollectionMode.Upi && string.IsNullOrWhiteSpace(input.UpiReference) ? "NOT_GIVEN" : input.UpiReference;
            delivery.RecordCollection(cash, mode, reference, input.RiderId, input.DeliveredAt, requireExactAmount: false);
        }

        if (cash != delivery.AmountPaise || delivery.PaidPaise != cash)
        {
            await issues.OpenAsync(
                IssueKind.CollectionDifference,
                IssueSubjectType.Payment,
                delivery.Id,
                delivery.OrderId,
                delivery.FulfillmentLocationId,
                new { amountDuePaise = delivery.AmountPaise, collectedPaise = cash, shipmentId = input.ShipmentId },
                cancellationToken);
        }
    }

    /// <summary>PAY-H7 / PAY-R10: money captured for a closed (or already paid) order → full CANCELLATION refund, auto-approved.</summary>
    public async Task OnPaymentReceivedForClosedOrderAsync(Guid orderId, Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await payments.GetAsync(paymentId, cancellationToken);
        if (payment is null || payment.OrderId != orderId || !payment.IsPaid)
        {
            return;
        }

        await CreateSystemRefundAsync(payment, RefundReason.Cancellation, payment.RefundablePaise, $"ClosedOrder:{paymentId}", requiresApproval: false, null, cancellationToken);
    }

    /// <summary>PAY-H8: complaint decided with resolution REFUND → COMPLAINT refund REQUESTED by the decider.</summary>
    public async Task OnComplaintDecidedAsync(ComplaintDecidedInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!string.Equals(input.Resolution, "REFUND", StringComparison.Ordinal) || input.RefundAmountPaise is not > 0)
        {
            return;
        }

        var attempts = await payments.ListByOrderAsync(input.OrderId, cancellationToken);
        var paid = attempts.FirstOrDefault(p => p.IsPaid && !p.IsExcess);
        var sourceReference = $"Complaint:{input.ComplaintId}";
        if (paid is null || paid.Refunds.Any(r => r.SourceReference == sourceReference))
        {
            return;
        }

        var supportsRefunds = providers.CapabilitiesOf(paid.Provider).HasFlag(ProviderCapabilities.ProviderRefunds);
        RefundMethod method;
        if (input.RefundMethod is not null && EnumCodes.TryToEnum<RefundMethod>(input.RefundMethod, out var given) && given != RefundMethod.Upi)
        {
            method = given;
        }
        else if (input.RefundMethod is null && supportsRefunds)
        {
            method = RefundMethod.Original;
        }
        else
        {
            // Q5: a refund on a delivery payment needs staff to choose how (and a UPI id); flag it instead of guessing.
            await issues.OpenAsync(
                IssueKind.RefundMethodRequired,
                IssueSubjectType.Order,
                input.OrderId,
                input.OrderId,
                paid.FulfillmentLocationId,
                new { complaintId = input.ComplaintId, amountPaise = input.RefundAmountPaise, refundMethod = input.RefundMethod },
                cancellationToken);
            return;
        }

        var amount = Math.Min(input.RefundAmountPaise.Value, paid.RefundablePaise);
        if (amount <= 0 || (method == RefundMethod.Original && !supportsRefunds))
        {
            return;
        }

        paid.RequestRefund(
            new RefundDraft(amount, RefundReason.Complaint, method, input.DecidedBy, input.ComplaintId, null, null, sourceReference),
            supportsRefunds,
            clock.UtcNow);
    }

    private static Payment? PaidOnline(IReadOnlyList<Payment> attempts)
        => attempts.FirstOrDefault(p => p.Method == PaymentMethod.Online && p.IsPaid && !p.IsExcess);

    private void CancelOpenAttempts(IReadOnlyList<Payment> attempts)
    {
        var now = clock.UtcNow;
        foreach (var attempt in attempts.Where(p => p.IsOpenAttempt))
        {
            attempt.Cancel(now);
        }
    }

    private async Task FollowAmountDueAsync(IReadOnlyList<Payment> attempts, long amountDuePaise, CancellationToken cancellationToken)
    {
        var delivery = attempts.FirstOrDefault(p => p.IsDeliveryCollected && p.Status is PaymentStatus.Pending or PaymentStatus.Collected);
        if (delivery is null)
        {
            return;
        }

        if (delivery.Status == PaymentStatus.Pending)
        {
            delivery.UpdateAmountDue(amountDuePaise);
        }
        else if (delivery.PaidPaise != amountDuePaise)
        {
            await issues.OpenAsync(
                IssueKind.CollectionDifference,
                IssueSubjectType.Payment,
                delivery.Id,
                delivery.OrderId,
                delivery.FulfillmentLocationId,
                new { amountDuePaise, collectedPaise = delivery.PaidPaise },
                cancellationToken);
        }
    }

    private async Task CreateSystemRefundAsync(Payment paid, RefundReason reason, long amountPaise, string sourceReference, bool requiresApproval, Guid? requestedBy, CancellationToken cancellationToken)
    {
        var result = paid.CreateSystemRefund(reason, amountPaise, sourceReference, clock.UtcNow, requiresApproval, requestedBy);
        if (result is { WasCapped: true })
        {
            await issues.OpenAsync(
                IssueKind.RefundCapped,
                IssueSubjectType.Payment,
                paid.Id,
                paid.OrderId,
                paid.FulfillmentLocationId,
                new { reasonCode = reason.ToCode(), requestedPaise = result.RequestedPaise, grantedPaise = result.GrantedPaise, sourceReference },
                cancellationToken);
        }
    }
}
