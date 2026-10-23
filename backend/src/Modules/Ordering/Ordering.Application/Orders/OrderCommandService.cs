using FreshOrbit.Cart.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Payments.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Ordering.Application.Orders;

/// <summary>
/// Order commands after placement: customer cancel / substitution answers / reorder and staff start-preparing, pack,
/// substitutions, cancel and goodwill (docs/product/ordering.md §5). Staff commands check the If-Match version.
/// </summary>
public sealed class OrderCommandService(
    IOrderingTransactions transactions,
    IUnitOfWork unitOfWork,
    OrderQueryService queries,
    SubstitutionPricer substitutionPricer,
    IInventoryApi inventory,
    IOrderStockReleaser stockReleaser,
    IPaymentsApi payments,
    ISettingsApi settings,
    ICartApi carts,
    OrderingOptions options,
    IClock clock)
{
    public async Task<OrderDetailResponse> CancelByCustomerAsync(Guid customerId, Guid orderId, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ParseRequired<CustomerCancelReason>(request.ReasonCode, "reasonCode");
        await transactions.RunAsync(
            async ct =>
            {
                var order = await queries.GetOwnedAsync(customerId, orderId, ct);
                var policy = await settings.GetOrderPolicyAsync(order.CityId, ct);
                order.CancelByCustomer(reason, OrderQueryService.CancelLimit(policy), clock.UtcNow);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
        return await queries.GetCustomerOrderAsync(customerId, orderId, cancellationToken);
    }

    /// <summary>Customer accept / reject of a PROPOSED substitution (ORD-R35..R37); retried after a lost race.</summary>
    public async Task<OrderDetailResponse> AnswerSubstitutionAsync(Guid customerId, Guid orderId, Guid substitutionId, bool accept, CancellationToken cancellationToken)
    {
        await transactions.RunAsync(
            async ct =>
            {
                var order = await queries.GetOwnedAsync(customerId, orderId, ct);
                await AnswerAsync(order, substitutionId, accept, OrderActor.Customer(customerId), ct);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
        return await queries.GetCustomerOrderAsync(customerId, orderId, cancellationToken);
    }

    /// <summary>ORD-R59: the order's lines go into the customer's cart via Cart.Public; unsellable lines are named in messages.</summary>
    public async Task<CartView> ReorderAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await queries.GetOwnedAsync(customerId, orderId, cancellationToken);
        var lines = order.Items
            .Where(i => i.Status != OrderItemStatus.Substituted)
            .OrderBy(i => i.LineNo)
            .Select(i => new CartLineToAdd(i.VariantId, i.PreparationMethodId, i.OrderedQuantity))
            .ToList();
        return await carts.AddLinesAsync(customerId, order.Pincode, lines, cancellationToken);
    }

    public async Task<AdminOrderDetailResponse> StartPreparingAsync(ICurrentActor actor, Guid orderId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        order.StartPreparing(OrderActor.Staff(actor.StaffMemberId), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await queries.AdminDetailAsync(actor, orderId, cancellationToken);
    }

    /// <summary>Weigh and pack (ORD-R25..R32): Ordering rows and OrderPacked only; never refuses for stock.</summary>
    public async Task<PackResponse> PackAsync(ICurrentActor actor, Guid orderId, uint expectedVersion, PackOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = (request.Lines ?? []).Select((l, i) => l?.PackedQuantity is null
            ? throw new RefusalException(OrderingErrors.PackedQuantityInvalid, "Some packed quantities are invalid.", [new FieldError($"lines[{i}]", OrderingErrors.PackedQuantityInvalid, "Send the packed quantity with its unit.")])
            : new PackLine(l.OrderItemId, l.PackedQuantity.Value, l.PackedQuantity.Unit)).ToList();
        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        var paid = order.IsPrepaid ? (await payments.GetPaymentStatusAsync(order.Id, cancellationToken)).PaidPaise : 0;
        var result = order.Pack(lines, request.ColdBoxUsed, Math.Max(paid, order.CapturedPaise), OrderActor.Staff(actor.StaffMemberId), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderViews.Pack(order, result);
    }

    /// <summary>ORD-R33, R34: propose; AUTO_SIMILAR within the guard is accepted at once (stock moved, D6 exception).</summary>
    public async Task<AdminOrderDetailResponse> ProposeSubstitutionAsync(ICurrentActor actor, Guid orderId, Guid orderItemId, uint expectedVersion, ProposeSubstitutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Quantity is null)
        {
            throw new RefusalException(new FieldError("quantity", OrderingErrors.ValidationFailed, "Send the proposed quantity."));
        }

        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        var now = clock.UtcNow;
        var policy = await settings.GetOrderPolicyAsync(order.CityId, cancellationToken);
        var (snapshot, name) = await substitutionPricer.PriceAsync(order, IdGenerator.NewId(now), request.VariantId, request.PreparationMethodId, request.Quantity.Value, policy.MaxOverweightBp, now, cancellationToken);
        if (!string.Equals(snapshot.QuantityUnit, request.Quantity.Unit, StringComparison.Ordinal))
        {
            throw new RefusalException(new FieldError("quantity.unit", OrderingErrors.ValidationFailed, $"Send the quantity in {snapshot.QuantityUnit}."));
        }

        var (substitution, autoAccept) = order.ProposeSubstitution(orderItemId, snapshot, name, options.SubstitutionMinutes, now);
        if (autoAccept)
        {
            await AcceptAsync(order, substitution, snapshot, OrderActor.System, now, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await queries.AdminDetailAsync(actor, orderId, cancellationToken);
    }

    /// <summary>Staff record the answer taken by phone (CALL_ME): <c>ACCEPTED</c> or <c>REJECTED</c>.</summary>
    public async Task<AdminOrderDetailResponse> RecordSubstitutionResponseAsync(ICurrentActor actor, Guid orderId, Guid substitutionId, uint expectedVersion, RecordSubstitutionResponseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var accept = request.Answer switch
        {
            "ACCEPTED" or "ACCEPT" => true,
            "REJECTED" or "REJECT" => false,
            _ => throw new RefusalException(new FieldError("answer", OrderingErrors.ValidationFailed, "Answer ACCEPTED or REJECTED.")),
        };
        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        await AnswerAsync(order, substitutionId, accept, OrderActor.Staff(actor.StaffMemberId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await queries.AdminDetailAsync(actor, orderId, cancellationToken);
    }

    public async Task<AdminOrderDetailResponse> CancelByStaffAsync(ICurrentActor actor, Guid orderId, uint expectedVersion, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var reason = ParseRequired<StaffCancelReason>(request.ReasonCode, "reasonCode");
        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        order.CancelByStaff(reason, actor.StaffMemberId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await queries.AdminDetailAsync(actor, orderId, cancellationToken);
    }

    /// <summary>ORD-R58 goodwill adjustment.</summary>
    public async Task<AdminOrderDetailResponse> GoodwillAsync(ICurrentActor actor, Guid orderId, uint expectedVersion, GoodwillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var order = await LoadForStaffAsync(actor, orderId, expectedVersion, cancellationToken);
        var payment = await payments.GetPaymentStatusAsync(order.Id, cancellationToken);
        order.AddGoodwill(request.AmountPaise, request.Reason ?? string.Empty, request.OrderItemId, actor.StaffMemberId ?? actor.UserId, payment.PaidPaise > 0, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await queries.AdminDetailAsync(actor, orderId, cancellationToken);
    }

    private static TEnum ParseRequired<TEnum>(string? code, string field)
        where TEnum : struct, Enum
        => EnumCodes.TryToEnum<TEnum>(code?.Trim(), out var value)
            ? value
            : throw new RefusalException(new FieldError(field, OrderingErrors.ValidationFailed, $"Choose one of {string.Join(", ", EnumCodes.AllCodes<TEnum>())}."));

    private async Task<Order> LoadForStaffAsync(ICurrentActor actor, Guid orderId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var order = await queries.GetForStaffAsync(actor, orderId, cancellationToken);
        unitOfWork.SetExpectedVersion(order, expectedVersion);
        return order;
    }

    private async Task AnswerAsync(Order order, Guid substitutionId, bool accept, OrderActor actor, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (!accept)
        {
            order.RejectSubstitution(substitutionId, actor, now);
            return;
        }

        var substitution = order.PendingSubstitution(substitutionId, SubstitutionStatus.Accepted);
        if (substitution is null)
        {
            return;
        }

        if (now >= substitution.ExpiresAt)
        {
            throw new DomainException(OrderingErrors.SubstitutionExpired, "The substitution offer has expired.");
        }

        var policy = await settings.GetOrderPolicyAsync(order.CityId, cancellationToken);
        var (snapshot, _) = await substitutionPricer.PriceAsync(order, IdGenerator.NewId(now), substitution.ProposedVariantId, substitution.ProposedPreparationMethodId, substitution.ProposedQuantity, policy.MaxOverweightBp, now, cancellationToken);
        await AcceptAsync(order, substitution, snapshot, actor, now, cancellationToken);
    }

    /// <summary>ORD-R35 in one transaction: reserve the new line, accept on the order, release the original line.</summary>
    private async Task AcceptAsync(Order order, Substitution substitution, OrderItemSnapshot snapshot, OrderActor actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reservation = await inventory.ReserveAsync(
            new ReserveStockRequest(order.Id, order.FulfillmentLocationId, [new ReserveStockLine(snapshot.ItemId, snapshot.VariantId, snapshot.GrossQuantity)], null),
            cancellationToken);
        if (!reservation.Succeeded)
        {
            throw new DomainException(OrderingErrors.OutOfStock, "The substitute is out of stock.");
        }

        var newItem = order.AcceptSubstitution(substitution.Id, snapshot, actor, now);
        if (newItem is not null)
        {
            await stockReleaser.ReleaseAsync(order.Id, substitution.OrderItemId, cancellationToken);
        }
    }
}
