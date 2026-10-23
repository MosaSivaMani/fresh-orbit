using System.Globalization;
using FreshOrbit.Customers.Public.Events;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Public;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Notifications.Application.Handlers;

// Event adapters (docs/product/notifications.md §4): customer messages for order, payment and delivery events. Each
// queues through INotificationsApi with the event id as DedupeKey (NOT-R4) and never saves; the outbox dispatcher
// saves the queued messages with the inbox row. Variables a payload cannot give render as empty text (NOT-R5).

/// <summary>Shared formatting and queuing for the customer notification adapters.</summary>
public sealed class CustomerNotifier(INotificationsApi notifications, ISettingsApi settings)
{
    public Task QueueAsync(Guid customerId, string templateCode, Dictionary<string, string> data, DomainEvent source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        return notifications.QueueForCustomerAsync(
            new QueueCustomerNotificationRequest(customerId, templateCode, data, null, source.EventId.ToString("N")),
            cancellationToken);
    }

    /// <summary>The business time zone (Settings business profile); UTC until the profile is saved.</summary>
    public async Task<TimeZoneInfo> TimeZoneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await settings.GetBusinessProfileAsync(cancellationToken);
            return TimeZoneInfo.TryFindSystemTimeZoneById(profile.TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;
        }
        catch (DomainException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static string Money(long paise)
        => string.Create(CultureInfo.InvariantCulture, $"{(paise < 0 ? "-" : string.Empty)}\u20B9{Math.Abs(paise) / 100m:N2}");

    public static string DateText(DateTimeOffset at, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(at, zone).ToString("ddd d MMM", CultureInfo.InvariantCulture);

    public static string TimeText(DateTimeOffset at, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(at, zone).ToString("h:mm tt", CultureInfo.InvariantCulture);

    public static string WindowText(DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone)
        => $"{TimeText(start, zone)} - {TimeText(end, zone)}";

    public static string PaymentMethodText(string? code) => code switch
    {
        "COD" => "Cash on delivery",
        "UPI_ON_DELIVERY" => "UPI on delivery",
        "ONLINE" => "Paid online",
        _ => Humanize(code),
    };

    public static string QuantityText(int quantity, string stockUnit)
        => stockUnit == "GRAM" ? string.Create(CultureInfo.InvariantCulture, $"{quantity} g") : string.Create(CultureInfo.InvariantCulture, $"{quantity} pcs");

    /// <summary>UPPER_SNAKE code as a sentence fragment: CUSTOMER_UNAVAILABLE → "Customer unavailable".</summary>
    public static string Humanize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var words = code.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}

public sealed class NotifyOrderPlaced(CustomerNotifier notifier) : IEventHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.Status != "CONFIRMED")
        {
            return; // ONLINE orders are confirmed by OrderConfirmed after payment.
        }

        var zone = await notifier.TimeZoneAsync(cancellationToken);
        await notifier.QueueAsync(domainEvent.CustomerId, "ORDER_CONFIRMED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["slotDateText"] = CustomerNotifier.DateText(domainEvent.SlotStartAt, zone),
            ["slotWindowText"] = CustomerNotifier.WindowText(domainEvent.SlotStartAt, domainEvent.SlotEndAt, zone),
            ["estimatedTotalText"] = CustomerNotifier.Money(domainEvent.EstimatedTotalPaise),
            ["paymentMethodText"] = CustomerNotifier.PaymentMethodText(domainEvent.PaymentMethod),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderConfirmed(CustomerNotifier notifier, IOrderingApi orders) : IEventHandler<OrderConfirmed>
{
    public async Task HandleAsync(OrderConfirmed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var summary = await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken);
        await notifier.QueueAsync(domainEvent.CustomerId, "ORDER_CONFIRMED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["estimatedTotalText"] = summary is null ? string.Empty : CustomerNotifier.Money(summary.TotalPaise),
            ["paymentMethodText"] = CustomerNotifier.PaymentMethodText("ONLINE"),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderStatusChanged(CustomerNotifier notifier) : IEventHandler<OrderStatusChanged>
{
    public Task HandleAsync(OrderStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent.To switch
        {
            "PREPARING" => notifier.QueueAsync(domainEvent.CustomerId, "ORDER_PREPARING", new(StringComparer.Ordinal) { ["orderNumber"] = domainEvent.OrderNumber }, domainEvent, cancellationToken),
            "DELIVERY_FAILED" => notifier.QueueAsync(domainEvent.CustomerId, "DELIVERY_FAILED", new(StringComparer.Ordinal)
            {
                ["orderNumber"] = domainEvent.OrderNumber,
                ["reasonText"] = CustomerNotifier.Humanize(domainEvent.ReasonCode),
            }, domainEvent, cancellationToken),
            _ => Task.CompletedTask,
        };
    }
}

public sealed class NotifyOrderPacked(CustomerNotifier notifier) : IEventHandler<OrderPacked>
{
    public Task HandleAsync(OrderPacked domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "ORDER_PACKED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["finalTotalText"] = CustomerNotifier.Money(domainEvent.FinalTotalPaise),
            ["amountDueText"] = CustomerNotifier.Money(domainEvent.AmountDuePaise),
            ["weightNoteText"] = domainEvent.RefundDuePaise > 0 ? $"{CustomerNotifier.Money(domainEvent.RefundDuePaise)} will be refunded for the weight difference." : string.Empty,
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderDispatched(CustomerNotifier notifier, IDeliveryApi delivery, IOrderingApi orders) : IEventHandler<OrderDispatched>
{
    public async Task HandleAsync(OrderDispatched domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var shipment = await delivery.GetShipmentForOrderAsync(domainEvent.OrderId, cancellationToken);
        var summary = await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken);
        await notifier.QueueAsync(domainEvent.CustomerId, "ORDER_OUT_FOR_DELIVERY", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["riderName"] = shipment?.RiderName ?? string.Empty,
            ["riderPhone"] = shipment?.RiderPhone ?? string.Empty,
            ["deliveryCode"] = shipment?.DeliveryCode ?? string.Empty,
            ["amountDueText"] = summary is null ? string.Empty : CustomerNotifier.Money(summary.AmountDuePaise),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderDelivered(CustomerNotifier notifier) : IEventHandler<OrderDelivered>
{
    public Task HandleAsync(OrderDelivered domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "ORDER_DELIVERED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["invoiceNumber"] = domainEvent.InvoiceNumber,
            ["finalTotalText"] = CustomerNotifier.Money(domainEvent.FinalTotalPaise),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderCancelled(CustomerNotifier notifier) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "ORDER_CANCELLED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["reasonText"] = CustomerNotifier.Humanize(domainEvent.ReasonCode),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyOrderPaymentFailed(CustomerNotifier notifier) : IEventHandler<OrderPaymentFailed>
{
    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "ORDER_CANCELLED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["reasonText"] = domainEvent.Reason == "PAYMENT_TIMEOUT" ? "Payment was not completed in time" : "Payment failed",
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifySubstitutionProposed(CustomerNotifier notifier) : IEventHandler<SubstitutionProposed>
{
    public async Task HandleAsync(SubstitutionProposed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var zone = await notifier.TimeZoneAsync(cancellationToken);
        await notifier.QueueAsync(domainEvent.CustomerId, "SUBSTITUTION_PROPOSED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["itemName"] = domainEvent.OriginalName,
            ["proposedItemName"] = domainEvent.ProposedName,
            ["proposedQuantityText"] = CustomerNotifier.QuantityText(domainEvent.ProposedQuantity, domainEvent.StockUnit),
            ["priceDifferenceText"] = CustomerNotifier.Money(domainEvent.PriceDifferencePaise),
            ["respondByText"] = CustomerNotifier.TimeText(domainEvent.ExpiresAt, zone),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyComplaintRaised(CustomerNotifier notifier) : IEventHandler<ComplaintRaised>
{
    public Task HandleAsync(ComplaintRaised domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "COMPLAINT_RECEIVED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["complaintKindText"] = CustomerNotifier.Humanize(domainEvent.Kind),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyComplaintDecided(CustomerNotifier notifier) : IEventHandler<ComplaintDecided>
{
    public Task HandleAsync(ComplaintDecided domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.QueueAsync(domainEvent.CustomerId, "COMPLAINT_RESOLVED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = domainEvent.OrderNumber,
            ["resolutionText"] = CustomerNotifier.Humanize(domainEvent.Resolution),
            ["refundAmountText"] = domainEvent.RefundAmountPaise is { } refund ? CustomerNotifier.Money(refund) : string.Empty,
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyPaymentCaptured(CustomerNotifier notifier, IOrderingApi orders) : IEventHandler<PaymentCaptured>
{
    public async Task HandleAsync(PaymentCaptured domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken) is not { } order)
        {
            return;
        }

        await notifier.QueueAsync(order.CustomerId, "PAYMENT_RECEIVED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = order.OrderNumber,
            ["amountText"] = CustomerNotifier.Money(domainEvent.AmountPaise),
            ["paymentMethodText"] = CustomerNotifier.PaymentMethodText("ONLINE"),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyPaymentFailed(CustomerNotifier notifier, IOrderingApi orders) : IEventHandler<PaymentFailed>
{
    public async Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.HasOtherActiveAttempt || await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken) is not { } order)
        {
            return;
        }

        await notifier.QueueAsync(order.CustomerId, "PAYMENT_FAILED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = order.OrderNumber,
            ["amountText"] = CustomerNotifier.Money(order.AmountDuePaise),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyRefundApproved(CustomerNotifier notifier, IOrderingApi orders) : IEventHandler<RefundApproved>
{
    public async Task HandleAsync(RefundApproved domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken) is not { } order)
        {
            return;
        }

        await notifier.QueueAsync(order.CustomerId, "REFUND_INITIATED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = order.OrderNumber,
            ["amountText"] = CustomerNotifier.Money(domainEvent.AmountPaise),
            ["refundMethodText"] = CustomerNotifier.Humanize(domainEvent.Method),
        }, domainEvent, cancellationToken);
    }
}

public sealed class NotifyRefundSucceeded(CustomerNotifier notifier, IOrderingApi orders) : IEventHandler<RefundSucceeded>
{
    public async Task HandleAsync(RefundSucceeded domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (await orders.GetOrderSummaryAsync(domainEvent.OrderId, cancellationToken) is not { } order)
        {
            return;
        }

        await notifier.QueueAsync(order.CustomerId, "REFUND_SUCCEEDED", new(StringComparer.Ordinal)
        {
            ["orderNumber"] = order.OrderNumber,
            ["amountText"] = CustomerNotifier.Money(domainEvent.AmountPaise),
            ["refundMethodText"] = CustomerNotifier.Humanize(domainEvent.Method),
        }, domainEvent, cancellationToken);
    }
}

/// <summary><c>CustomerDeleted</c>: the account's push device tokens are removed (no more pushes to its devices).</summary>
public sealed class DropDeviceTokensOnCustomerDeleted(INotificationsRepository repository) : IEventHandler<CustomerDeleted>
{
    public async Task HandleAsync(CustomerDeleted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        foreach (var token in await repository.GetDeviceTokensAsync(domainEvent.UserId, cancellationToken))
        {
            repository.Remove(token);
        }
    }
}
