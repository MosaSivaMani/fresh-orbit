using System.Globalization;
using AwesomeAssertions;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Infrastructure.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>Payments and Delivery event handler bodies (the merge adapters call these), run the way the dispatcher does.</summary>
public sealed class OrderEventHandlingTests(OrderingDatabase database) : OrderingTestBase(database)
{
    private static readonly DateTimeOffset DeliveredAt = Start.AddHours(6);

    [Fact]
    public async Task Customer_deletion_is_blocked_by_an_active_order_until_it_is_delivered()
    {
        var orderId = (await SeedOutForDeliveryAsync(1))[0];
        var customerId = (await LoadOrderAsync(orderId))!.CustomerId;
        var blocking = () => InScopeAsync((s, _) => s.GetRequiredService<OrderingCustomerDeletionCheck>().GetBlockingItemsAsync(customerId, Ct));

        (await blocking()).Should().Equal("ACTIVE_ORDERS_EXIST");
        await DeliverAsync(orderId);
        (await blocking()).Should().BeEmpty();
    }

    [Fact]
    public async Task Invoice_numbers_stay_gapless_when_deliveries_commit_concurrently_and_one_rolls_back()
    {
        var orderIds = await SeedOutForDeliveryAsync(20);

        var rolledBack = RollBackAfterDeliveringAsync(orderIds[0]);
        var deliveries = orderIds.Skip(1).Select(id => Task.Run(() => DeliverAsync(id), Ct));
        await Task.WhenAll([rolledBack, .. deliveries]);
        (await DeliverAsync(orderIds[0])).Should().Be(DeliveryEventOutcome.Applied);

        await using var db = Database.CreateDbContext();
        var invoices = await db.Set<Invoice>().AsNoTracking().Where(i => orderIds.Contains(i.OrderId)).ToListAsync(Ct);
        invoices.Should().HaveCount(20);
        var numbers = invoices.Select(i => int.Parse(i.InvoiceNumber[(i.InvoiceNumber.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture)).Order().ToList();
        numbers.Should().OnlyHaveUniqueItems();
        (numbers[^1] - numbers[0]).Should().Be(19, "numbers are contiguous: the rolled back attempt did not consume one");
        (await NumberSeries.PeekAsync(db, "INVOICE", SettingsSeed.Period, Ct)).Should().Be(numbers[^1] + 1);
        var orders = await db.Set<Order>().AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToListAsync(Ct);
        orders.Should().OnlyContain(o => o.Status == OrderStatus.Delivered);
    }

    [Fact]
    public async Task Delivered_twice_issues_one_invoice_and_one_status_change()
    {
        var orderId = (await SeedOutForDeliveryAsync(1))[0];

        (await DeliverAsync(orderId)).Should().Be(DeliveryEventOutcome.Applied);
        (await DeliverAsync(orderId)).Should().Be(DeliveryEventOutcome.AlreadyApplied);

        await using var db = Database.CreateDbContext();
        (await db.Set<Invoice>().CountAsync(i => i.OrderId == orderId, Ct)).Should().Be(1);
        (await OutboxAsync("OrderDelivered", orderId)).Should().ContainSingle();
        (await OutboxAsync("InvoiceIssued", orderId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Dispatch_after_cancel_is_rejected_without_change()
    {
        var orderId = await PlaceOrderAsync();
        await AsHandlerAsync(async s =>
        {
            var order = await s.GetRequiredService<FreshOrbitDbContext>().Set<Order>().Include(o => o.Items).FirstAsync(o => o.Id == orderId, Ct);
            order.CancelByCustomer(CustomerCancelReason.ChangedMind, OrderStatus.Confirmed, Start);
            return true;
        });

        var outcome = await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDispatchedAsync(orderId, Guid.NewGuid(), null, Start.AddHours(4), Ct));

        outcome.Should().Be(DeliveryEventOutcome.Rejected);
        (await LoadOrderAsync(orderId))!.Status.Should().Be(OrderStatus.Cancelled);
        (await OutboxAsync("OrderDispatched", orderId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Payment_captured_after_expiry_keeps_payment_failed_and_flags_the_payment_once()
    {
        var orderId = await PlaceOrderAsync("ONLINE");
        var expiry = () => InScopeAsync((s, _) => s.GetRequiredService<OrderExpiryService>().ExpireUnpaidOrdersAsync(Ct));
        (await expiry()).Should().Be(0, "nothing is due yet");

        Factory.Clock.Advance(TimeSpan.FromMinutes(16));
        (await expiry()).Should().BeGreaterThanOrEqualTo(1, "other classes' unpaid orders may fall due too");
        (await expiry()).Should().Be(0);
        (await LoadOrderAsync(orderId))!.Status.Should().Be(OrderStatus.PaymentFailed);
        var failed = (await OutboxAsync("OrderPaymentFailed", orderId)).Should().ContainSingle().Subject;
        failed.GetProperty("reason").GetString().Should().Be("PAYMENT_TIMEOUT");

        var paymentId = Guid.NewGuid();
        var outcome = await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnPaymentCapturedAsync(orderId, paymentId, World.EstimatedTotalPaise, Factory.Clock.UtcNow, Ct));

        outcome.Should().Be(PaymentCaptureOutcome.ClosedOrderPayment);
        (await LoadOrderAsync(orderId))!.Status.Should().Be(OrderStatus.PaymentFailed);
        var flagged = (await OutboxAsync("PaymentReceivedForClosedOrder", orderId)).Should().ContainSingle().Subject;
        flagged.GetProperty("paymentId").GetGuid().Should().Be(paymentId);
        (await OutboxAsync("OrderConfirmed", orderId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Payment_capture_confirms_once_and_failure_before_due_is_ignored()
    {
        var orderId = await PlaceOrderAsync("ONLINE");
        (await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnPaymentFailedAsync(orderId, Guid.NewGuid(), "DECLINED", false, Ct))).Should().BeFalse();
        var paymentId = Guid.NewGuid();
        (await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnPaymentCapturedAsync(orderId, paymentId, World.EstimatedTotalPaise, Start.AddMinutes(2), Ct))).Should().Be(PaymentCaptureOutcome.Confirmed);
        (await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnPaymentCapturedAsync(orderId, paymentId, World.EstimatedTotalPaise, Start.AddMinutes(2), Ct))).Should().Be(PaymentCaptureOutcome.AlreadyConfirmed);

        (await LoadOrderAsync(orderId))!.Status.Should().Be(OrderStatus.Confirmed);
        (await OutboxAsync("OrderConfirmed", orderId)).Should().ContainSingle();
    }

    private async Task<List<Guid>> SeedOutForDeliveryAsync(int count)
    {
        await using var db = Database.CreateDbContext();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], fulfillmentLocationId: CheckoutWorld.LocationId, placedAt: Start).Preparing().PackedAsOrdered();
            order.Dispatch(Guid.NewGuid(), null, Start.AddHours(4));
            db.Add(order);
            ids.Add(order.Id);
        }

        await db.SaveChangesAsync(Ct);
        return ids;
    }

    private Task<DeliveryEventOutcome> DeliverAsync(Guid orderId)
        => AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDeliveredAsync(orderId, Guid.NewGuid(), DeliveredAt, Ct));

    /// <summary>A handler that takes an invoice number and then fails: its transaction rolls back with the number.</summary>
    private Task<bool> RollBackAfterDeliveringAsync(Guid orderId)
        => InScopeAsync(async (s, db) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await s.GetRequiredService<OrderEventService>().OnShipmentDeliveredAsync(orderId, Guid.NewGuid(), DeliveredAt, Ct);
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
            db.ChangeTracker.Clear();
            return true;
        });
}
