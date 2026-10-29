using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Ordering.Application.Events;

/// <summary>
/// What Ordering does when Payments or Delivery report a fact (docs/product/ordering.md §4 Consumed, ORD-R43..R52).
/// Each method changes tracked rows only: the outbox dispatcher saves them with its inbox row (handlers must not
/// save). Merge adapters (<c>IEventHandler&lt;PaymentCaptured&gt;</c>, …) map the event fields to these parameters.
/// </summary>
public sealed partial class OrderEventService(
    IOrderRepository orders,
    IInvoiceRepository invoices,
    ISettingsApi settings,
    OrderingOptions options,
    IClock clock,
    ILogger<OrderEventService> logger)
{
    public const string InvoiceSeries = "INVOICE";

    /// <summary><c>PaymentCaptured</c> (ORD-R43).</summary>
    public async Task<PaymentCaptureOutcome?> OnPaymentCapturedAsync(Guid orderId, Guid paymentId, long amountPaise, DateTimeOffset capturedAt, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            LogUnknownOrder(orderId, "PaymentCaptured");
            return null;
        }

        return order.ConfirmPayment(paymentId, amountPaise, capturedAt);
    }

    /// <summary><c>PaymentFailed</c> (ORD-R44): PAYMENT_FAILED only when due and no other attempt is active.</summary>
    public async Task<bool> OnPaymentFailedAsync(Guid orderId, Guid paymentId, string? failureCode, bool hasOtherActiveAttempt, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            LogUnknownOrder(orderId, "PaymentFailed");
            return false;
        }

        var failed = order.FailPayment(hasOtherActiveAttempt, clock.UtcNow);
        if (!failed)
        {
            LogPaymentFailureIgnored(orderId, paymentId, failureCode ?? string.Empty);
        }

        return failed;
    }

    /// <summary><c>ShipmentDispatched</c>: PACKED → OUT_FOR_DELIVERY (ORD-R46).</summary>
    public async Task<DeliveryEventOutcome> OnShipmentDispatchedAsync(Guid orderId, Guid shipmentId, Guid? riderId, DateTimeOffset dispatchedAt, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        return Report(order, order.Dispatch(shipmentId, riderId, dispatchedAt), "ShipmentDispatched");
    }

    /// <summary>
    /// <c>ShipmentDelivered</c> (ORD-R48..R52): OUT_FOR_DELIVERY → DELIVERED with the invoice in the same transaction.
    /// The INVOICE number series row is locked by Settings in this transaction, so numbers are gapless.
    /// </summary>
    public async Task<DeliveryEventOutcome> OnShipmentDeliveredAsync(Guid orderId, Guid shipmentId, DateTimeOffset deliveredAt, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        var check = order.CheckDeliver();
        if (check != DeliveryEventOutcome.Applied)
        {
            return Report(order, check, "ShipmentDelivered");
        }

        if (await invoices.FindByOrderAsync(order.Id, cancellationToken) is not null)
        {
            return DeliveryEventOutcome.AlreadyApplied;
        }

        var profile = await settings.GetBusinessProfileAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(profile.Gstin))
        {
            // Checkout refuses orders while the GSTIN is missing (D295); this guards a profile edited after the order.
            throw new DomainException(OrderingErrors.ConfigurationMissing, "The business gstin is not configured; invoices cannot be issued.", "gstin");
        }

        var number = await settings.NextNumberAsync(InvoiceSeries, deliveredAt, cancellationToken);
        var invoice = Invoice.Issue(order, number, new InvoiceSeller(profile.LegalName, profile.Gstin), options.DeliveryServiceCode, clock.UtcNow);
        invoices.Add(invoice);
        return order.Deliver(shipmentId, invoice.Id, invoice.InvoiceNumber, deliveredAt);
    }

    /// <summary><c>DeliveryAttemptFailed</c>: OUT_FOR_DELIVERY → DELIVERY_FAILED.</summary>
    public async Task<DeliveryEventOutcome> OnDeliveryAttemptFailedAsync(Guid orderId, int attemptNo, string? outcome, DateTimeOffset attemptedAt, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        _ = attemptNo;
        return Report(order, order.FailDeliveryAttempt(outcome, attemptedAt), "DeliveryAttemptFailed");
    }

    /// <summary><c>ShipmentReattempted</c>: DELIVERY_FAILED → OUT_FOR_DELIVERY.</summary>
    public async Task<DeliveryEventOutcome> OnShipmentReattemptedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        return Report(order, order.Reattempt(clock.UtcNow), "ShipmentReattempted");
    }

    /// <summary><c>ShipmentReturned</c>: DELIVERY_FAILED → RETURNED.</summary>
    public async Task<DeliveryEventOutcome> OnShipmentReturnedAsync(Guid orderId, Guid shipmentId, DateTimeOffset returnedAt, CancellationToken cancellationToken)
    {
        var order = await RequireAsync(orderId, cancellationToken);
        return Report(order, order.Return(shipmentId, returnedAt), "ShipmentReturned");
    }

    private async Task<Order> RequireAsync(Guid orderId, CancellationToken cancellationToken)
        => await orders.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException($"Order {orderId} does not exist.");

    /// <summary>Q9: an event that does not fit the status (e.g. dispatch after cancel) changes nothing and is logged.</summary>
    private DeliveryEventOutcome Report(Order order, DeliveryEventOutcome outcome, string eventName)
    {
        if (outcome == DeliveryEventOutcome.Rejected)
        {
            LogEventRejected(eventName, order.Id, order.Status);
        }

        return outcome;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ORDER_EVENT_REJECTED: {EventName} for order {OrderId} in status {Status} was ignored")]
    private partial void LogEventRejected(string eventName, Guid orderId, OrderStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{EventName} for unknown order {OrderId} was ignored")]
    private partial void LogUnknownOrder(Guid orderId, string eventName);

    [LoggerMessage(Level = LogLevel.Information, Message = "PaymentFailed {PaymentId} ({FailureCode}) for order {OrderId} left the order unchanged")]
    private partial void LogPaymentFailureIgnored(Guid orderId, Guid paymentId, string failureCode);
}
