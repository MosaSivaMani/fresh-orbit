using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Paging;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Reads;

/// <summary>Paging and filter parameters shared by the admin lists.</summary>
public sealed record ListParameters(int Page, int PageSize, DateOnly? FromDate, DateOnly? ToDate, Guid? FulfillmentLocationId)
{
    public int Skip => (Page - 1) * PageSize;

    public void Validate()
    {
        var errors = new List<FieldError>();
        if (Page < 1)
        {
            errors.Add(new FieldError("page", "PAGE_OUT_OF_RANGE", "Page must be 1 or more."));
        }

        if (PageSize is < 1 or > PagedRequest.MaxPageSize)
        {
            errors.Add(new FieldError("pageSize", "PAGE_SIZE_OUT_OF_RANGE", $"Page size must be between 1 and {PagedRequest.MaxPageSize}."));
        }

        if (FromDate > ToDate)
        {
            errors.Add(new FieldError("fromDate", PaymentsErrors.ValidationFailed, "From date must not be after to date."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }
}

/// <summary>Parses repeated UPPER_SNAKE query values (<c>status=CAPTURED&amp;status=COLLECTED</c>).</summary>
public static class QueryCodes
{
    public static IReadOnlyList<TEnum> Parse<TEnum>(string[]? values, string field)
        where TEnum : struct, Enum
    {
        var parsed = new List<TEnum>();
        foreach (var value in values ?? [])
        {
            if (!EnumCodes.TryToEnum<TEnum>(value, out var code))
            {
                throw new RefusalException(new FieldError(field, PaymentsErrors.ValidationFailed, $"'{value}' is not a valid {field}; use one of {string.Join(", ", EnumCodes.AllCodes<TEnum>())}."));
            }

            parsed.Add(code);
        }

        return parsed;
    }
}

/// <summary>Admin list and detail queries (one SQL each, location-scoped by PAY-R23).</summary>
public sealed class PaymentsReads(FreshOrbitDbContext db, ICurrentActor actor)
{
    public async Task<PagedResponse<PaymentResponse>> ListPaymentsAsync(ListParameters list, IReadOnlyList<PaymentStatus> statuses, IReadOnlyList<PaymentMethod> methods, string? provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        var query = Scope(db.Set<Payment>().AsNoTracking().Include(p => p.Refunds), list.FulfillmentLocationId, p => p.FulfillmentLocationId);
        if (statuses.Count > 0)
        {
            query = query.Where(p => statuses.Contains(p.Status));
        }

        if (methods.Count > 0)
        {
            query = query.Where(p => methods.Contains(p.Method));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            var key = provider.ToUpperInvariant();
            query = query.Where(p => p.Provider == key);
        }

        query = InRange(query, list);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(p => EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt)).ThenByDescending(p => p.Id)
            .Skip(list.Skip).Take(list.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<PaymentResponse>([.. items.Select(p => p.ToResponse())], list.Page, list.PageSize, total);
    }

    public async Task<PaymentResponse> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("PAYMENT_NOT_FOUND", "No payment has this id.");
        LocationGuard.EnsureAccess(actor, payment.FulfillmentLocationId);
        return payment.ToResponse();
    }

    public async Task<OrderPaymentsResponse> GetOrderPaymentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payments = await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).Where(p => p.OrderId == orderId).OrderBy(p => p.Id).ToListAsync(cancellationToken);
        if (payments.Count > 0)
        {
            LocationGuard.EnsureAccess(actor, payments[0].FulfillmentLocationId);
        }

        return new OrderPaymentsResponse(
            orderId,
            [.. payments.Select(p => p.ToResponse())],
            [.. payments.SelectMany(p => p.Refunds.OrderBy(r => r.RequestedAt).Select(r => r.ToResponse(p, maskUpi: true)))]);
    }

    public async Task<PagedResponse<RefundResponse>> ListRefundsAsync(ListParameters list, IReadOnlyList<RefundStatus> statuses, IReadOnlyList<RefundReason> reasons, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        var query = Scope(db.Set<Refund>().AsNoTracking(), list.FulfillmentLocationId, r => r.FulfillmentLocationId);
        if (statuses.Count > 0)
        {
            query = query.Where(r => statuses.Contains(r.Status));
        }

        if (reasons.Count > 0)
        {
            query = query.Where(r => reasons.Contains(r.ReasonCode));
        }

        if (list.FromDate is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(r => r.RequestedAt >= fromUtc);
        }

        if (list.ToDate is { } to)
        {
            var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(r => r.RequestedAt < toUtc);
        }

        var total = await query.CountAsync(cancellationToken);
        var refunds = await query.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).Skip(list.Skip).Take(list.PageSize).ToListAsync(cancellationToken);
        var paymentIds = refunds.Select(r => r.PaymentId).Distinct().ToList();
        var payments = await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).Where(p => paymentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        return new PagedResponse<RefundResponse>([.. refunds.Select(r => r.ToResponse(payments[r.PaymentId], maskUpi: true))], list.Page, list.PageSize, total);
    }

    public async Task<PagedResponse<WebhookEventResponse>> ListWebhookEventsAsync(ListParameters list, string? provider, bool? signatureValid, bool? processed, IReadOnlyList<WebhookEventStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (LocationGuard.Scope(actor) is not null)
        {
            // Webhook events are not tied to a location: only all-locations staff may list them (PAY-R23).
            throw new LocationAccessDeniedException();
        }

        var query = db.Set<WebhookEvent>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var key = provider.ToUpperInvariant();
            query = query.Where(e => e.Provider == key);
        }

        if (signatureValid is { } valid)
        {
            query = query.Where(e => e.SignatureValid == valid);
        }

        if (processed is { } isProcessed)
        {
            query = isProcessed ? query.Where(e => e.ProcessedAt != null) : query.Where(e => e.ProcessedAt == null);
        }

        if (statuses.Count > 0)
        {
            query = query.Where(e => statuses.Contains(e.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.Id).Skip(list.Skip).Take(list.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<WebhookEventResponse>([.. items.Select(e => e.ToResponse())], list.Page, list.PageSize, total);
    }

    public async Task<PagedResponse<CashSettlementResponse>> ListSettlementsAsync(ListParameters list, Guid? riderId, DateOnly? date, IReadOnlyList<CashSettlementStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        var query = Scope(db.Set<CashSettlement>().AsNoTracking(), list.FulfillmentLocationId, s => s.FulfillmentLocationId);
        if (riderId is { } rider)
        {
            query = query.Where(s => s.RiderId == rider);
        }

        if (date is { } day)
        {
            query = query.Where(s => s.SettlementDate == day);
        }

        if (list.FromDate is { } from)
        {
            query = query.Where(s => s.SettlementDate >= from);
        }

        if (list.ToDate is { } to)
        {
            query = query.Where(s => s.SettlementDate <= to);
        }

        if (statuses.Count > 0)
        {
            query = query.Where(s => statuses.Contains(s.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(s => s.SettlementDate).ThenByDescending(s => s.Id).Skip(list.Skip).Take(list.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<CashSettlementResponse>([.. items.Select(s => s.ToResponse())], list.Page, list.PageSize, total);
    }

    public async Task<PagedResponse<PaymentIssueResponse>> ListIssuesAsync(ListParameters list, IReadOnlyList<IssueKind> kinds, IReadOnlyList<IssueStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        var query = db.Set<ReconciliationIssue>().AsNoTracking();
        if (list.FulfillmentLocationId is { } locationId)
        {
            LocationGuard.EnsureAccess(actor, locationId);
            query = query.Where(i => i.FulfillmentLocationId == locationId);
        }

        if (LocationGuard.Scope(actor) is { } scope)
        {
            query = query.Where(i => i.FulfillmentLocationId != null && scope.Contains(i.FulfillmentLocationId.Value));
        }

        if (kinds.Count > 0)
        {
            query = query.Where(i => kinds.Contains(i.Kind));
        }

        if (statuses.Count > 0)
        {
            query = query.Where(i => statuses.Contains(i.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(i => i.DetectedAt).ThenByDescending(i => i.Id).Skip(list.Skip).Take(list.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<PaymentIssueResponse>([.. items.Select(i => i.ToResponse())], list.Page, list.PageSize, total);
    }

    private IQueryable<T> Scope<T>(IQueryable<T> query, Guid? fulfillmentLocationId, System.Linq.Expressions.Expression<Func<T, Guid>> location)
    {
        if (fulfillmentLocationId is { } requested)
        {
            LocationGuard.EnsureAccess(actor, requested);
            query = query.Where(Equal(location, requested));
        }

        if (LocationGuard.Scope(actor) is { } scope)
        {
            var parameter = location.Parameters[0];
            var contains = System.Linq.Expressions.Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [typeof(Guid)],
                System.Linq.Expressions.Expression.Constant(scope.ToList()),
                location.Body);
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, parameter));
        }

        return query;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Equal<T>(System.Linq.Expressions.Expression<Func<T, Guid>> location, Guid value)
        => System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.Equal(location.Body, System.Linq.Expressions.Expression.Constant(value)),
            location.Parameters[0]);

    private static IQueryable<Payment> InRange(IQueryable<Payment> query, ListParameters list)
    {
        if (list.FromDate is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(p => EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt) >= fromUtc);
        }

        if (list.ToDate is { } to)
        {
            var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(p => EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt) < toUtc);
        }

        return query;
    }
}
