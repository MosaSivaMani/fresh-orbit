using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Settlements;

/// <summary>Records a COD / UPI-on-delivery collection from the staff route (PAY-R4; decision D14).</summary>
public sealed class CashCollectionService(
    IShipmentFacts shipments,
    IOrderingApi ordering,
    IPaymentRepository payments,
    IPaymentsSession session,
    ICurrentActor actor,
    IClock clock)
{
    public async Task<PaymentResponse> RecordAsync(Guid shipmentId, CashCollectedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await shipments.GetShipmentAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("SHIPMENT_NOT_FOUND", "No shipment has this id.");
        LocationGuard.EnsureAccess(actor, shipment.FulfillmentLocationId);
        var order = await ordering.GetOrderSummaryAsync(shipment.OrderId, cancellationToken)
            ?? throw new NotFoundException("ORDER_NOT_FOUND", "No order has this id.");
        if (order.Status is not (OrderStatus.OutForDelivery or OrderStatus.Delivered))
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Cash can be recorded only for an order out for delivery or delivered.");
        }

        return await SaveRetry.RunAsync(session, async () =>
        {
            var attempts = await payments.ListByOrderAsync(order.OrderId, cancellationToken);
            var payment = attempts.Where(p => p.IsDeliveryCollected).OrderByDescending(p => p.Status == PaymentStatus.Collected).FirstOrDefault()
                ?? throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "This order is not paid on delivery.");
            payment.RecordCollection(request.AmountPaise, request.Mode, request.UpiReference, shipment.RiderId, clock.UtcNow, requireExactAmount: true);
            await session.SaveChangesAsync(cancellationToken);
            return payment.ToResponse();
        });
    }
}

/// <summary>Rider cash settlements (PAY-R19, R20; G2).</summary>
public sealed class CashSettlementService(
    IShipmentFacts shipments,
    IPaymentRepository payments,
    ICashSettlementRepository settlements,
    IssueRecorder issues,
    IPaymentsSession session,
    ICurrentActor actor,
    IClock clock)
{
    public async Task<CashSettlementResponse> CreateAsync(CreateCashSettlementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = LocationGuard.RequireUser(actor);
        var rider = await shipments.GetRiderAsync(request.RiderId, cancellationToken)
            ?? throw new NotFoundException("RIDER_NOT_FOUND", "No rider has this id.");
        LocationGuard.EnsureAccess(actor, rider.FulfillmentLocationId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(rider.TimeZone);
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        if (request.Date > today)
        {
            throw new RefusalException(new FieldError("date", PaymentsErrors.DateInFuture, "Date must not be in the future."));
        }

        if (await settlements.ExistsAsync(request.RiderId, request.Date, cancellationToken))
        {
            throw new ConflictException(PaymentsErrors.SettlementExists, "A settlement for this rider and date already exists.", "date");
        }

        var (fromUtc, toUtc) = LocalDay(request.Date, zone);
        var collections = await payments.ListUnsettledCashAsync(request.RiderId, fromUtc, toUtc, cancellationToken);
        var expected = collections.Sum(p => p.PaidPaise);
        var settlement = CashSettlement.Create(request.RiderId, rider.FulfillmentLocationId, request.Date, expected, request.ReceivedPaise, collections.Count, user, request.Note, now);
        settlements.Add(settlement);
        foreach (var payment in collections)
        {
            payment.AssignToSettlement(settlement.Id);
        }

        if (settlement.DifferencePaise != 0)
        {
            await issues.OpenAsync(
                IssueKind.CashDifference,
                IssueSubjectType.Settlement,
                settlement.Id,
                null,
                rider.FulfillmentLocationId,
                new { riderId = rider.RiderId, date = request.Date, expectedPaise = expected, receivedPaise = request.ReceivedPaise, settlement.DifferencePaise },
                cancellationToken);
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueViolationException)
        {
            throw new ConflictException(PaymentsErrors.SettlementExists, "A settlement for this rider and date already exists.", "date");
        }

        return settlement.ToResponse();
    }

    public Task<CashSettlementResponse> DisputeAsync(Guid id, uint expectedVersion, DisputeCashSettlementRequest request, CancellationToken cancellationToken)
        => ChangeAsync(id, expectedVersion, (settlement, _) => settlement.Dispute(request?.Reason ?? string.Empty), cancellationToken);

    public Task<CashSettlementResponse> SettleAsync(Guid id, uint expectedVersion, SettleCashSettlementRequest request, CancellationToken cancellationToken)
        => ChangeAsync(id, expectedVersion, (settlement, user) => settlement.Settle(request?.Note ?? string.Empty, user, clock.UtcNow), cancellationToken);

    public async Task<CashSettlementResponse> GetAsync(Guid id, CancellationToken cancellationToken)
        => (await LoadAsync(id, cancellationToken)).ToResponse();

    /// <summary>[local midnight of <paramref name="date"/>, next local midnight) in UTC.</summary>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) LocalDay(DateOnly date, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var from = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        return (new DateTimeOffset(from, TimeSpan.Zero), new DateTimeOffset(to, TimeSpan.Zero));
    }

    private async Task<CashSettlementResponse> ChangeAsync(Guid id, uint expectedVersion, Action<CashSettlement, Guid> change, CancellationToken cancellationToken)
    {
        var user = LocationGuard.RequireUser(actor);
        var settlement = await LoadAsync(id, cancellationToken);
        session.SetExpectedVersion(settlement, expectedVersion);
        change(settlement, user);
        await session.SaveChangesAsync(cancellationToken);
        return settlement.ToResponse();
    }

    private async Task<CashSettlement> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await settlements.GetAsync(id, cancellationToken)
            ?? throw new NotFoundException("CASH_SETTLEMENT_NOT_FOUND", "No cash settlement has this id.");
        LocationGuard.EnsureAccess(actor, settlement.FulfillmentLocationId);
        return settlement;
    }
}

/// <summary>Staff resolution of reconciliation issues (G3).</summary>
public sealed class PaymentIssueService(IReconciliationIssueRepository issues, IPaymentsSession session, ICurrentActor actor, IClock clock)
{
    public async Task<PaymentIssueResponse> ResolveAsync(Guid id, uint expectedVersion, ResolveIssueRequest request, CancellationToken cancellationToken)
    {
        var user = LocationGuard.RequireUser(actor);
        var issue = await issues.GetAsync(id, cancellationToken)
            ?? throw new NotFoundException("PAYMENT_ISSUE_NOT_FOUND", "No payment issue has this id.");
        if (issue.FulfillmentLocationId is { } locationId)
        {
            LocationGuard.EnsureAccess(actor, locationId);
        }

        session.SetExpectedVersion(issue, expectedVersion);
        issue.Resolve(user, request?.Note ?? string.Empty, clock.UtcNow);
        await session.SaveChangesAsync(cancellationToken);
        return issue.ToResponse();
    }
}
