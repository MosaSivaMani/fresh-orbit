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
        => db.Set<Complaint>().Include(c => c.Photos).FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);

    public Task<bool> HasOpenAsync(Guid orderId, Guid? orderItemId, CancellationToken cancellationToken)
        => db.Set<Complaint>().AnyAsync(
            c => c.OrderId == orderId && c.OrderItemId == orderItemId && (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Approved),
            cancellationToken);

    public void Add(Complaint complaint) => db.Add(complaint);

    public void MarkChanged(Complaint complaint)
    {
        var entry = db.Entry(complaint);
        if (entry.State == EntityState.Unchanged)
        {
            entry.Property(AuditColumns.ModifiedAt).IsModified = true;
        }
    }
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
                o.TimeZone,
            })
            .ToListAsync(cancellationToken);
        return ([.. items.Select(o => new OrderListItem(o.Id, o.OrderNumber, o.Status.ToCode(), o.PaymentMethod.ToCode(), o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.ItemCount, o.TotalPaise, o.FinalTotalPaise, o.AmountDuePaise)
        {
            TimeZone = OrderViews.TimeZoneOf(o.OrderNumber, o.TimeZone),
        })], total);
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

        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(o => statuses.Contains(o.Status));
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
                o.Version,
            })
            .ToListAsync(cancellationToken);
        return ([.. items.Select(o => new AdminOrderListItem(o.Id, o.OrderNumber, o.Status.ToCode(), o.PaymentMethod.ToCode(), o.CustomerId, o.RecipientName, o.Phone, o.FulfillmentLocationId, o.DeliverySlotId, o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.ItemCount, o.TotalPaise, o.FinalTotalPaise, o.AmountDuePaise, OrderViews.VersionOf(o.Version)))], total);
    }

    public async Task<PickListData> PickListAsync(Guid fulfillmentLocationId, Guid slotId, CancellationToken cancellationToken)
    {
        var rows =
            from o in db.Set<Order>().AsNoTracking()
            join i in db.Set<OrderItem>().AsNoTracking() on o.Id equals i.OrderId
            where o.FulfillmentLocationId == fulfillmentLocationId
                && o.DeliverySlotId == slotId
                && (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing)
                && i.Status == OrderItemStatus.Active
            select new { i.OrderId, i.VariantId, i.ProductName, i.VariantName, i.Sku, i.PreparationCode, i.PreparationName, i.StockUnit, i.GrossQuantity };
        var lines = await (
            from r in rows
            group new { r.GrossQuantity, r.OrderId } by new { r.VariantId, r.ProductName, r.VariantName, r.Sku, r.PreparationCode, r.PreparationName, r.StockUnit } into g
            select new PickListLine(
                g.Key.VariantId,
                g.Key.ProductName,
                g.Key.VariantName,
                g.Key.PreparationCode,
                g.Key.PreparationName,
                g.Key.StockUnit,
                g.Sum(x => (long)x.GrossQuantity),
                g.Select(x => x.OrderId).Distinct().Count(),
                g.Key.Sku))
            .ToListAsync(cancellationToken);
        var orderCount = await rows.Select(r => r.OrderId).Distinct().CountAsync(cancellationToken);
        return new PickListData(
            [.. lines.OrderBy(l => l.ProductName, StringComparer.Ordinal).ThenBy(l => l.VariantName, StringComparer.Ordinal).ThenBy(l => l.PreparationCode, StringComparer.Ordinal)],
            orderCount);
    }

    public async Task<IReadOnlyList<ComplaintResponse>> ListComplaintsForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var complaints = await db.Set<Complaint>().AsNoTracking().Include(c => c.Photos).Where(c => c.OrderId == orderId).OrderBy(c => c.RaisedAt).ToListAsync(cancellationToken);
        return await WithFactsAsync(complaints, ComplaintAudience.Customer, cancellationToken);
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
        var items = await query.Include(c => c.Photos).OrderByDescending(c => c.RaisedAt).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync(cancellationToken);
        return (await WithFactsAsync(items, ComplaintAudience.Staff, cancellationToken), total);
    }

    /// <summary>G15 facts for a page of complaints: one query for the lines, one for the orders (never per complaint).</summary>
    private async Task<IReadOnlyList<ComplaintResponse>> WithFactsAsync(List<Complaint> complaints, ComplaintAudience audience, CancellationToken cancellationToken)
    {
        if (complaints.Count == 0)
        {
            return [];
        }

        var itemIds = complaints.Where(c => c.OrderItemId is not null).Select(c => c.OrderItemId!.Value).Distinct().ToList();
        var orderIds = complaints.Select(c => c.OrderId).Distinct().ToList();
        var lines = await db.Set<OrderItem>().AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new LineFacts(i.Id, i.ProductName, i.VariantName, i.PreparationName, i.FinalTotalPaise, i.FinalDiscountPaise))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var totals = await db.Set<Order>().AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .Select(o => new OrderTotals(o.Id, o.FinalTotalPaise, o.TotalPaise))
            .ToDictionaryAsync(o => o.Id, cancellationToken);
        return [.. complaints.Select(c => ComplaintResponse.From(c, FactsOf(c, lines, totals), audience))];
    }

    private static ComplaintFacts FactsOf(Complaint complaint, Dictionary<Guid, LineFacts> lines, Dictionary<Guid, OrderTotals> totals)
    {
        if (complaint.OrderItemId is { } itemId && lines.TryGetValue(itemId, out var line))
        {
            return new ComplaintFacts(line.ProductName, line.VariantName, line.PreparationName, ComplaintRefundLimit.ForLine(line.FinalTotalPaise, line.FinalDiscountPaise));
        }

        return totals.TryGetValue(complaint.OrderId, out var order)
            ? new ComplaintFacts(null, null, null, ComplaintRefundLimit.ForOrder(order.FinalTotalPaise, order.TotalPaise))
            : new ComplaintFacts(null, null, null, 0);
    }

    private sealed record LineFacts(Guid Id, string ProductName, string VariantName, string? PreparationName, long? FinalTotalPaise, long? FinalDiscountPaise);

    private sealed record OrderTotals(Guid Id, long? FinalTotalPaise, long TotalPaise);
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
