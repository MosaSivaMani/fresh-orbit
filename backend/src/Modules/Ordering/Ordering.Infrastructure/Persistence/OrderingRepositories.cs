using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Complaints;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Ordering.Infrastructure.Persistence;

internal sealed class OrderRepository(FreshOrbitDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken)
        => WithChildren().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
        => WithChildren().FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken)
        => db.Set<Order>().AnyAsync(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.PaymentFailed, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListUnpaidDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
        => await db.Set<Order>()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.PaymentDueAt <= now)
            .OrderBy(o => o.PaymentDueAt)
            .Select(o => o.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Guid OrderId, Guid SubstitutionId)>> ListExpiredSubstitutionsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
    {
        var rows = await db.Set<Substitution>()
            .Where(s => s.Status == SubstitutionStatus.Proposed && s.ExpiresAt <= now)
            .OrderBy(s => s.ExpiresAt)
            .Select(s => new { s.OrderId, s.Id })
            .Take(take)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => (r.OrderId, r.Id))];
    }

    public void Add(Order order) => db.Add(order);

    private IQueryable<Order> WithChildren() => db.Set<Order>()
        .Include(o => o.Items)
        .Include(o => o.History)
        .Include(o => o.Adjustments)
        .Include(o => o.Substitutions)
        .AsSplitQuery();
}

internal sealed class ComplaintRepository(FreshOrbitDbContext db) : IComplaintRepository
{
    public Task<Complaint?> GetAsync(Guid complaintId, CancellationToken cancellationToken)
        => db.Set<Complaint>().FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);

    public Task<bool> HasOpenAsync(Guid orderId, Guid? orderItemId, CancellationToken cancellationToken)
        => db.Set<Complaint>().AnyAsync(
            c => c.OrderId == orderId && c.OrderItemId == orderItemId && (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Approved),
            cancellationToken);

    public void Add(Complaint complaint) => db.Add(complaint);
}

internal sealed class InvoiceRepository(FreshOrbitDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => db.Set<Invoice>().Include(i => i.Lines).FirstOrDefaultAsync(i => i.OrderId == orderId, cancellationToken);

    public void Add(Invoice invoice) => db.Add(invoice);
}

internal sealed class OrderReads(FreshOrbitDbContext db) : IOrderReads
{
    public async Task<(IReadOnlyList<OrderListItem> Items, int Total)> ListForCustomerAsync(Guid customerId, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Set<Order>().AsNoTracking().Where(o => o.CustomerId == customerId);
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.PaymentMethod,
                o.PlacedAt,
                o.SlotStartAt,
                o.SlotEndAt,
                ItemCount = o.Items.Count(i => i.Status != OrderItemStatus.Substituted),
                o.TotalPaise,
                o.FinalTotalPaise,
                o.AmountDuePaise,
            })
            .ToListAsync(cancellationToken);
        return ([.. items.Select(o => new OrderListItem(o.Id, o.OrderNumber, o.Status.ToCode(), o.PaymentMethod.ToCode(), o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.ItemCount, o.TotalPaise, o.FinalTotalPaise, o.AmountDuePaise))], total);
    }

    public async Task<(IReadOnlyList<AdminOrderListItem> Items, int Total)> ListForStaffAsync(AdminOrderFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = db.Set<Order>().AsNoTracking();
        if (filter.LocationIds is { } allowed)
        {
            var ids = allowed.ToList();
            query = query.Where(o => ids.Contains(o.FulfillmentLocationId));
        }

        if (filter.FulfillmentLocationId is { } locationId)
        {
            query = query.Where(o => o.FulfillmentLocationId == locationId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        if (filter.PaymentMethod is { } method)
        {
            query = query.Where(o => o.PaymentMethod == method);
        }

        if (filter.SlotId is { } slotId)
        {
            query = query.Where(o => o.DeliverySlotId == slotId);
        }

        if (filter.SlotFrom is { } from && filter.SlotUntil is { } until)
        {
            query = query.Where(o => o.SlotStartAt >= from && o.SlotStartAt < until);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var pattern = $"%{filter.Q.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal)}%";
            query = query.Where(o => EF.Functions.ILike(o.OrderNumber, pattern) || EF.Functions.ILike(o.Phone, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(o => o.SlotStartAt)
            .ThenBy(o => o.PlacedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.PaymentMethod,
                o.CustomerId,
                o.RecipientName,
                o.Phone,
                o.FulfillmentLocationId,
                o.DeliverySlotId,
                o.PlacedAt,
                o.SlotStartAt,
                o.SlotEndAt,
                ItemCount = o.Items.Count(i => i.Status != OrderItemStatus.Substituted),
                o.TotalPaise,
                o.FinalTotalPaise,
                o.AmountDuePaise,
            })
            .ToListAsync(cancellationToken);
        return ([.. items.Select(o => new AdminOrderListItem(o.Id, o.OrderNumber, o.Status.ToCode(), o.PaymentMethod.ToCode(), o.CustomerId, o.RecipientName, o.Phone, o.FulfillmentLocationId, o.DeliverySlotId, o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.ItemCount, o.TotalPaise, o.FinalTotalPaise, o.AmountDuePaise))], total);
    }

    public async Task<IReadOnlyList<PickListLine>> PickListAsync(Guid fulfillmentLocationId, Guid slotId, CancellationToken cancellationToken)
    {
        var lines = await (
            from o in db.Set<Order>().AsNoTracking()
            join i in db.Set<OrderItem>().AsNoTracking() on o.Id equals i.OrderId
            where o.FulfillmentLocationId == fulfillmentLocationId
                && o.DeliverySlotId == slotId
                && (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing)
                && i.Status == OrderItemStatus.Active
            group new { i.GrossQuantity, i.OrderId } by new { i.VariantId, i.ProductName, i.VariantName, i.PreparationCode, i.PreparationName, i.StockUnit } into g
            select new PickListLine(
                g.Key.VariantId,
                g.Key.ProductName,
                g.Key.VariantName,
                g.Key.PreparationCode,
                g.Key.PreparationName,
                g.Key.StockUnit,
                g.Sum(x => (long)x.GrossQuantity),
                g.Select(x => x.OrderId).Distinct().Count()))
            .ToListAsync(cancellationToken);
        return [.. lines.OrderBy(l => l.ProductName, StringComparer.Ordinal).ThenBy(l => l.VariantName, StringComparer.Ordinal).ThenBy(l => l.PreparationCode, StringComparer.Ordinal)];
    }

    public async Task<IReadOnlyList<ComplaintResponse>> ListComplaintsForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var complaints = await db.Set<Complaint>().AsNoTracking().Where(c => c.OrderId == orderId).OrderBy(c => c.RaisedAt).ToListAsync(cancellationToken);
        return [.. complaints.Select(ComplaintResponse.From)];
    }

    public async Task<(IReadOnlyList<ComplaintResponse> Items, int Total)> ListComplaintsAsync(ComplaintStatus? status, ComplaintKind? kind, Guid? fulfillmentLocationId, IReadOnlySet<Guid>? locationIds, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Set<Complaint>().AsNoTracking();
        if (locationIds is not null)
        {
            var ids = locationIds.ToList();
            query = query.Where(c => ids.Contains(c.FulfillmentLocationId));
        }

        if (fulfillmentLocationId is { } locationId)
        {
            query = query.Where(c => c.FulfillmentLocationId == locationId);
        }

        if (status is { } s)
        {
            query = query.Where(c => c.Status == s);
        }

        if (kind is { } k)
        {
            query = query.Where(c => c.Kind == k);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.RaisedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return ([.. items.Select(ComplaintResponse.From)], total);
    }
}

/// <summary>
/// Explicit transaction per attempt; a lost race (row version, unique violation, deadlock, serialization failure)
/// rolls back, clears the change tracker and repeats from Read: 3 retries, then CONCURRENT_SAVE (ORD-R13).
/// Inside an already open transaction (event handler, job) the attempt runs once, inline.
/// </summary>
internal sealed class OrderingTransactions(FreshOrbitDbContext db) : IOrderingTransactions
{
    public const int MaxRetries = 3;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (db.Database.CurrentTransaction is not null)
        {
            return await attempt(cancellationToken);
        }

        for (var retry = 0; ; retry++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await attempt(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsLostRace(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                if (retry >= MaxRetries)
                {
                    throw exception as ConcurrentSaveException ?? new ConcurrentSaveException(exception);
                }
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
    }

    internal static bool IsLostRace(Exception exception) => exception switch
    {
        ConcurrentSaveException => true,
        DbUpdateException { InnerException: PostgresException postgres } => IsRaceState(postgres.SqlState),
        PostgresException postgres => IsRaceState(postgres.SqlState),
        InvalidOperationException { InnerException: PostgresException postgres } => IsRaceState(postgres.SqlState),
        _ => false,
    };

    private static bool IsRaceState(string sqlState)
        => sqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure;
}
