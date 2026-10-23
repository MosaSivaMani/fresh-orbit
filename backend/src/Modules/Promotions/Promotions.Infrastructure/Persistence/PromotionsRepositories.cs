using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Promotions.Infrastructure.Persistence;

internal sealed class CouponRepository(FreshOrbitDbContext db) : ICouponRepository
{
    public Task<Coupon?> FindByCodeAsync(string code, bool track, CancellationToken cancellationToken)
    {
        var normalized = Coupon.NormalizeCode(code);
        var query = track ? db.Set<Coupon>() : db.Set<Coupon>().AsNoTracking();
        return query.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public Task<Coupon?> FindByIdAsync(Guid couponId, CancellationToken cancellationToken)
        => db.Set<Coupon>().FirstOrDefaultAsync(c => c.Id == couponId, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptCouponId, CancellationToken cancellationToken)
    {
        var normalized = Coupon.NormalizeCode(code);
        return db.Set<Coupon>().AnyAsync(c => c.Code == normalized && (exceptCouponId == null || c.Id != exceptCouponId), cancellationToken);
    }

    public Task<bool> HasRedemptionsAsync(Guid couponId, CancellationToken cancellationToken)
        => db.Set<CouponRedemption>().AnyAsync(r => r.CouponId == couponId, cancellationToken);

    public Task<int> CountAppliedAsync(Guid couponId, Guid customerId, CancellationToken cancellationToken)
        => db.Set<CouponRedemption>().CountAsync(r => r.CouponId == couponId && r.CustomerId == customerId && r.Status == RedemptionStatus.Applied, cancellationToken);

    public async Task<IReadOnlyList<Coupon>> ListDueForExpiryAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
        => await db.Set<Coupon>()
            .Where(c => (c.Status == CouponStatus.Active || c.Status == CouponStatus.Paused) && c.ValidTo != null && c.ValidTo <= now)
            .OrderBy(c => c.ValidTo)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void Add(Coupon coupon) => db.Add(coupon);
}

internal sealed class RedemptionRepository(FreshOrbitDbContext db) : IRedemptionRepository
{
    public Task<CouponRedemption?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => db.Set<CouponRedemption>().FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public void Add(CouponRedemption redemption) => db.Add(redemption);
}

internal sealed class PromotionRepository(FreshOrbitDbContext db) : IPromotionRepository
{
    public Task<Promotion?> FindByIdAsync(Guid promotionId, CancellationToken cancellationToken)
        => db.Set<Promotion>().FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);

    public void Add(Promotion promotion) => db.Add(promotion);
}

internal sealed class PromotionsReads(FreshOrbitDbContext db) : IPromotionsReads
{
    public async Task<(IReadOnlyList<CouponResponse> Items, int TotalCount)> ListCouponsAsync(CouponListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var coupons = db.Set<Coupon>().AsNoTracking();
        if (query.Status is { Count: > 0 } statuses)
        {
            coupons = coupons.Where(c => statuses.Contains(c.Status));
        }

        if (query.CityId is { } cityId)
        {
            coupons = coupons.Where(c => c.CityId == cityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim()}%";
            coupons = coupons.Where(c => EF.Functions.ILike(c.Code, pattern) || (c.Description != null && EF.Functions.ILike(c.Description, pattern)));
        }

        var total = await coupons.CountAsync(cancellationToken);
        var items = await coupons.OrderBy(c => c.Code).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return ([.. items.Select(CouponResponse.From)], total);
    }

    public async Task<(IReadOnlyList<RedemptionResponse> Items, int TotalCount)> ListRedemptionsAsync(Guid couponId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var redemptions = db.Set<CouponRedemption>().AsNoTracking().Where(r => r.CouponId == couponId);
        var total = await redemptions.CountAsync(cancellationToken);
        var items = await redemptions
            .OrderByDescending(r => r.RedeemedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RedemptionResponse(r.Id, r.OrderId, r.CustomerId, r.DiscountPaise, r.Status, r.RedeemedAt, r.ReversedAt, r.ReversalReason))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<PromotionResponse> Items, int TotalCount)> ListPromotionsAsync(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var promotions = db.Set<Promotion>().AsNoTracking();
        if (cityId is { } city)
        {
            promotions = promotions.Where(p => p.CityId == city);
        }

        var total = await promotions.CountAsync(cancellationToken);
        var items = await promotions.OrderBy(p => p.SortOrder).ThenByDescending(p => p.StartsAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return ([.. items.Select(PromotionResponse.From)], total);
    }

    public async Task<IReadOnlyList<Promotion>> ListShownPromotionsAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken)
        => await db.Set<Promotion>().AsNoTracking()
            .Where(p => p.IsActive && p.StartsAt <= at && (p.EndsAt == null || at < p.EndsAt) && (p.CityId == null || p.CityId == cityId))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.StartsAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Coupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var coupons = await db.Set<Coupon>().AsNoTracking()
            .Where(c => c.IsListed
                && c.Status == CouponStatus.Active
                && c.ValidFrom <= at
                && (c.ValidTo == null || at < c.ValidTo)
                && (c.CityId == null || c.CityId == cityId)
                && (c.TotalLimit == null || c.UsedCount < c.TotalLimit))
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
        if (customerId is not { } customer || coupons.All(c => c.PerCustomerLimit is null))
        {
            return coupons;
        }

        var ids = coupons.Where(c => c.PerCustomerLimit is not null).Select(c => c.Id).ToList();
        var used = await db.Set<CouponRedemption>().AsNoTracking()
            .Where(r => r.CustomerId == customer && r.Status == RedemptionStatus.Applied && ids.Contains(r.CouponId))
            .GroupBy(r => r.CouponId)
            .Select(g => new { CouponId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CouponId, x => x.Count, cancellationToken);
        return [.. coupons.Where(c => c.PerCustomerLimit is not { } limit || used.GetValueOrDefault(c.Id) < limit)];
    }
}
