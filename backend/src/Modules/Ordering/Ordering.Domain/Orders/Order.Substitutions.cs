using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Domain.Orders;

public sealed partial class Order
{
    public Substitution? FindSubstitution(Guid substitutionId) => _substitutions.Find(s => s.Id == substitutionId);

    /// <summary>
    /// Staff propose a substitute for an ACTIVE line (ORD-R33, R34). Returns the substitution and whether it is to be
    /// accepted at once (AUTO_SIMILAR and proposed max line total ≤ original max line total, Q6). A proposal waiting for
    /// the customer raises SubstitutionProposed and expires after <paramref name="substitutionMinutes"/>.
    /// </summary>
    public (Substitution Substitution, bool AutoAccept) ProposeSubstitution(
        Guid orderItemId,
        OrderItemSnapshot proposed,
        string proposedName,
        int substitutionMinutes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        var item = FindItem(orderItemId) ?? throw new NotFoundException("The order line does not exist.");
        if (Status != OrderStatus.Preparing)
        {
            throw NotAllowed("Substitutions can be proposed only while the order is being prepared.");
        }

        if (SubstitutionPreference == SubstitutionPreference.None)
        {
            throw NotAllowed("The customer asked for no substitutions.");
        }

        if (item.Status != OrderItemStatus.Active)
        {
            throw NotAllowed("Only an active line can be substituted.");
        }

        if (_substitutions.Exists(s => s.OrderItemId == item.Id && s.Status == SubstitutionStatus.Proposed))
        {
            throw NotAllowed("A substitution for this line is already waiting for an answer.");
        }

        if (proposed.VariantId == item.VariantId && proposed.PreparationMethodId == item.PreparationMethodId)
        {
            throw NotAllowed("Propose a different variant or preparation.");
        }

        var substitution = new Substitution(Id, item, proposed, proposedName, now, now.AddMinutes(substitutionMinutes));
        _substitutions.Add(substitution);
        Touch(now);
        var autoAccept = SubstitutionPreference == SubstitutionPreference.AutoSimilar && proposed.MaxLineTotalPaise <= item.MaxLineTotalPaise;
        if (!autoAccept)
        {
            Raise(new SubstitutionProposed(
                Id,
                OrderNumber,
                CustomerId,
                FulfillmentLocationId,
                substitution.Id,
                item.Id,
                item.Description,
                proposedName,
                proposed.Quantity,
                proposed.StockUnit,
                substitution.PriceDifferencePaise,
                substitution.ExpiresAt));
        }

        return (substitution, autoAccept);
    }

    /// <summary>
    /// Accept (ORD-R35, R37): PROPOSED and before <c>expires_at</c>. The original line becomes SUBSTITUTED, a new ACTIVE
    /// line takes the next line number with the fresh snapshot, and a SUBSTITUTION adjustment records the price
    /// difference. Returns the new line, or null when the substitution was already accepted (same answer repeated).
    /// </summary>
    public OrderItem? AcceptSubstitution(Guid substitutionId, OrderItemSnapshot fresh, OrderActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(actor);
        var substitution = PendingSubstitution(substitutionId, SubstitutionStatus.Accepted);
        if (substitution is null)
        {
            return null;
        }

        if (now >= substitution.ExpiresAt)
        {
            throw new DomainException(OrderingErrors.SubstitutionExpired, "The substitution offer has expired.");
        }

        var original = FindItem(substitution.OrderItemId)!;
        var lineNo = _items.Max(i => i.LineNo) + 1;
        var newItem = new OrderItem(Id, lineNo, fresh, original.Id);
        _items.Add(newItem);
        original.MarkStatus(OrderItemStatus.Substituted);
        var difference = newItem.EstimatedTotalPaise - original.EstimatedTotalPaise;
        substitution.Resolve(SubstitutionStatus.Accepted, actor.Type, newItem.Id, difference, now);
        var adjustment = new OrderAdjustment(Id, newItem.Id, AdjustmentKind.Substitution, difference, $"Substituted line {original.LineNo}", actor.Id, now);
        _adjustments.Add(adjustment);
        Touch(now);
        RaiseResolved(substitution, actor.Type, now);
        Raise(new OrderAdjusted(Id, OrderNumber, CustomerId, FulfillmentLocationId, adjustment.Id, AdjustmentKind.Substitution.ToCode(), difference, AmountDuePaise, RefundDuePaise, adjustment.Reason, actor.Id, now));
        return newItem;
    }

    /// <summary>Reject (ORD-R36, R37): line SHORT_SUPPLIED with a SHORT_SUPPLY adjustment; false when already rejected.</summary>
    public bool RejectSubstitution(Guid substitutionId, OrderActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var substitution = PendingSubstitution(substitutionId, SubstitutionStatus.Rejected);
        if (substitution is null)
        {
            return false;
        }

        ShortSupply(substitution, SubstitutionStatus.Rejected, actor, now);
        return true;
    }

    /// <summary>Expiry job (ORD-R36): a PROPOSED substitution past <c>expires_at</c> becomes EXPIRED.</summary>
    public bool ExpireSubstitution(Guid substitutionId, DateTimeOffset now)
    {
        var substitution = FindSubstitution(substitutionId);
        if (substitution is not { Status: SubstitutionStatus.Proposed } || now < substitution.ExpiresAt)
        {
            return false;
        }

        ShortSupply(substitution, SubstitutionStatus.Expired, OrderActor.System, now);
        return true;
    }

    private static DomainException NotAllowed(string message) => new(OrderingErrors.SubstitutionNotAllowed, message);

    /// <summary>
    /// The PROPOSED substitution; null when it already has <paramref name="answer"/> (same answer repeated, ORD-R37);
    /// 409 SUBSTITUTION_NOT_PENDING otherwise.
    /// </summary>
    public Substitution? PendingSubstitution(Guid substitutionId, SubstitutionStatus answer)
    {
        var substitution = FindSubstitution(substitutionId) ?? throw new NotFoundException("The substitution does not exist.");
        if (substitution.Status == answer)
        {
            return null;
        }

        if (substitution.Status != SubstitutionStatus.Proposed || Status != OrderStatus.Preparing)
        {
            throw new ConflictException(OrderingErrors.SubstitutionNotPending, "The substitution was already answered or has expired.");
        }

        return substitution;
    }

    private void ShortSupply(Substitution substitution, SubstitutionStatus status, OrderActor actor, DateTimeOffset now)
    {
        var item = FindItem(substitution.OrderItemId)!;
        item.MarkStatus(OrderItemStatus.ShortSupplied);
        substitution.Resolve(status, actor.Type, null, null, now);
        AddAdjustment(item.Id, AdjustmentKind.ShortSupply, -item.EstimatedTotalPaise, $"Substitution {status.ToCode()}", actor.Id, now);
        Touch(now);
        RaiseResolved(substitution, actor.Type, now);
    }

    private void RaiseResolved(Substitution substitution, ActorType actorType, DateTimeOffset now)
        => Raise(new SubstitutionResolved(
            Id,
            OrderNumber,
            CustomerId,
            FulfillmentLocationId,
            substitution.Id,
            substitution.OrderItemId,
            substitution.Status.ToCode(),
            actorType.ToCode(),
            substitution.NewOrderItemId,
            now));
}
