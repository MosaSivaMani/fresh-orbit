namespace FreshOrbit.Promotions.Public;

/// <summary>What other modules may ask the Promotions module.</summary>
public interface IPromotionsApi
{
    /// <summary>Checks a coupon against a cart and computes its discount. Read-only.</summary>
    Task<CouponEvaluation> EvaluateCouponAsync(CouponEvaluationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a redemption and increments usage. Tracked on the shared DbContext and saved by the caller; never
    /// saves. False when the coupon can no longer be used.
    /// </summary>
    Task<bool> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken = default);
}

public sealed record CouponEvaluationRequest(
    string Code,
    Guid? CustomerId,
    Guid CityId,
    IReadOnlyList<CouponLine> Lines,
    long ItemsPaise,
    long DeliveryFeePaise,
    bool IsFirstOrder);

public sealed record CouponLine(Guid ProductId, Guid CategoryId, long AmountPaise);

/// <param name="RefusalCode">UPPER_SNAKE reason when not valid (e.g. COUPON_EXPIRED, COUPON_MIN_ORDER_NOT_MET).</param>
public sealed record CouponEvaluation(bool IsValid, string? RefusalCode, long DiscountPaise, bool FreeDelivery);

public sealed record RedeemCouponRequest(string Code, Guid CustomerId, Guid OrderId, long DiscountPaise);
