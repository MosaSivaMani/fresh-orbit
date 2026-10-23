using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>Gross stock held for one order item. Changed only through its <see cref="StockItem"/>.</summary>
public sealed class StockReservation : Entity
{
    private StockReservation()
    {
    }

    internal StockReservation(Guid stockItemId, Guid orderId, Guid orderItemId, int quantity, DateTimeOffset? expiresAt)
    {
        StockItemId = stockItemId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        Quantity = quantity;
        ExpiresAt = expiresAt;
        Status = ReservationStatus.Active;
    }

    public Guid StockItemId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive => Status == ReservationStatus.Active;

    internal void ClearExpiry() => ExpiresAt = null;

    internal void Close(ReservationStatus status) => Status = status;
}
