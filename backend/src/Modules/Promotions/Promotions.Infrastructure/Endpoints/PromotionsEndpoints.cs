using System.Globalization;
using FreshOrbit.Locations.Public;
using FreshOrbit.Promotions.Application.Banners;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Promotions.Infrastructure.Endpoints;

/// <summary>Customer (<c>/coupons</c>, <c>/promotions</c>) and admin (<c>/admin/coupons</c>, <c>/admin/promotions</c>) endpoints.</summary>
internal static class PromotionsEndpoints
{
    public const string PromotionsManage = "promotions.manage";
    public const string CatalogManage = "catalog.manage";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/coupons", ListVisibleCouponsAsync).RequireAuthorization().WithTags("Promotions");
        endpoints.MapGet("/promotions", ListBannersAsync).WithTags("Promotions");

        var coupons = endpoints.MapGroup("/admin/coupons").RequirePermission(PromotionsManage).WithTags("Admin promotions");
        coupons.MapGet(string.Empty, ListCouponsAsync);
        coupons.MapPost(string.Empty, async (CouponRequest request, CouponAdminService service, HttpResponse response, CancellationToken ct) =>
        {
            var coupon = await service.CreateAsync(request, ct);
            SetETag(response, coupon.Version);
            return TypedResults.Created($"/api/v1/admin/coupons/{coupon.Id}", coupon);
        });
        coupons.MapGet("/{couponId:guid}", async (Guid couponId, CouponAdminService service, HttpResponse response, CancellationToken ct) =>
        {
            var coupon = await service.GetAsync(couponId, ct);
            SetETag(response, coupon.Version);
            return TypedResults.Ok(coupon);
        });
        coupons.MapPut("/{couponId:guid}", async (Guid couponId, CouponRequest request, HttpRequest http, CouponAdminService service, CancellationToken ct) =>
        {
            var coupon = await service.UpdateAsync(couponId, IfMatch.GetRequiredVersion(http), request, ct);
            SetETag(http.HttpContext.Response, coupon.Version);
            return TypedResults.Ok(coupon);
        });
        coupons.MapPost("/{couponId:guid}/pause", async (Guid couponId, HttpRequest http, CouponAdminService service, CancellationToken ct) =>
        {
            var coupon = await service.PauseAsync(couponId, IfMatch.GetRequiredVersion(http), ct);
            SetETag(http.HttpContext.Response, coupon.Version);
            return TypedResults.Ok(coupon);
        });
        coupons.MapPost("/{couponId:guid}/resume", async (Guid couponId, HttpRequest http, CouponAdminService service, CancellationToken ct) =>
        {
            var coupon = await service.ResumeAsync(couponId, IfMatch.GetRequiredVersion(http), ct);
            SetETag(http.HttpContext.Response, coupon.Version);
            return TypedResults.Ok(coupon);
        });
        coupons.MapGet("/{couponId:guid}/redemptions", async (Guid couponId, int? page, int? pageSize, CouponAdminService service, IPromotionsReads reads, CancellationToken ct) =>
        {
            await service.GetAsync(couponId, ct);
            var (p, size) = Paging(page, pageSize);
            var (items, total) = await reads.ListRedemptionsAsync(couponId, p, size, ct);
            return TypedResults.Ok(new PagedResponse<RedemptionResponse>(items, p, size, total));
        });

        var banners = endpoints.MapGroup("/admin/promotions").RequirePermission(CatalogManage).WithTags("Admin promotions");
        banners.MapGet(string.Empty, async (Guid? cityId, int? page, int? pageSize, IPromotionsReads reads, CancellationToken ct) =>
        {
            var (p, size) = Paging(page, pageSize);
            var (items, total) = await reads.ListPromotionsAsync(cityId, p, size, ct);
            return TypedResults.Ok(new PagedResponse<PromotionResponse>(items, p, size, total));
        });
        banners.MapPost(string.Empty, async (PromotionRequest request, BannerService service, HttpResponse response, CancellationToken ct) =>
        {
            var promotion = await service.CreateAsync(request, ct);
            SetETag(response, promotion.Version);
            return TypedResults.Created($"/api/v1/admin/promotions/{promotion.Id}", promotion);
        });
        banners.MapGet("/{promotionId:guid}", async (Guid promotionId, BannerService service, HttpResponse response, CancellationToken ct) =>
        {
            var promotion = await service.GetAsync(promotionId, ct);
            SetETag(response, promotion.Version);
            return TypedResults.Ok(promotion);
        });
        banners.MapPut("/{promotionId:guid}", async (Guid promotionId, PromotionRequest request, HttpRequest http, BannerService service, CancellationToken ct) =>
        {
            var promotion = await service.UpdateAsync(promotionId, IfMatch.GetRequiredVersion(http), request, ct);
            SetETag(http.HttpContext.Response, promotion.Version);
            return TypedResults.Ok(promotion);
        });
    }

    private static async Task<IResult> ListVisibleCouponsAsync(
        HttpRequest http,
        [FromQuery] string? pincode,
        ICurrentActor actor,
        [FromServices] ILocationsApi locations,
        IPromotionsApi promotions,
        IClock clock,
        CancellationToken ct)
    {
        if (actor.CustomerId is not { } customerId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Login required.",
                detail: "Sign in as a customer to see your coupons.",
                extensions: new Dictionary<string, object?> { ["code"] = "LOGIN_REQUIRED" });
        }

        var cityId = await ResolveCityAsync(http, pincode, locations, ct);
        var items = await promotions.ListVisibleCouponsAsync(cityId, customerId, clock.UtcNow, ct);
        return TypedResults.Ok(new PagedResponse<VisibleCoupon>(items, 1, Math.Max(items.Count, 1), items.Count));
    }

    private static async Task<IResult> ListBannersAsync(HttpRequest http, [FromQuery] string? pincode, [FromServices] ILocationsApi locations, [FromServices] IPromotionsApi promotions, IClock clock, CancellationToken ct)
    {
        var cityId = await ResolveCityAsync(http, pincode, locations, ct);
        var items = await promotions.GetActiveBannersAsync(cityId, clock.UtcNow, ct);
        return TypedResults.Ok(new PagedResponse<BannerInfo>(items, 1, Math.Max(items.Count, 1), items.Count));
    }

    private static async Task<IResult> ListCouponsAsync(
        [FromQuery] CouponStatus[]? status,
        Guid? cityId,
        string? q,
        int? page,
        int? pageSize,
        IPromotionsReads reads,
        CancellationToken ct)
    {
        var (p, size) = Paging(page, pageSize);
        var (items, total) = await reads.ListCouponsAsync(new CouponListQuery(status, cityId, q, p, size), ct);
        return TypedResults.Ok(new PagedResponse<CouponResponse>(items, p, size, total));
    }

    /// <summary>Optional location context: <c>?pincode=</c> wins over <c>X-Pincode</c>; unknown pincode → 422.</summary>
    private static async Task<Guid?> ResolveCityAsync(HttpRequest http, string? pincode, ILocationsApi locations, CancellationToken ct)
    {
        var value = !string.IsNullOrWhiteSpace(pincode) ? pincode.Trim() : http.Headers["X-Pincode"].ToString().Trim();
        if (value.Length == 0)
        {
            return null;
        }

        var serviceability = await locations.ResolvePincodeAsync(value, ct);
        return serviceability is { IsServiceable: true, CityId: { } cityId }
            ? cityId
            : throw new DomainException("PINCODE_NOT_SERVICEABLE", "We do not deliver to this pincode yet; enter another pincode.", "pincode");
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? PagedRequest.DefaultPageSize;
        var errors = new List<FieldError>();
        if (p < 1)
        {
            errors.Add(new FieldError("page", "VALIDATION_FAILED", "Page must be 1 or more."));
        }

        if (size is < 1 or > PagedRequest.MaxPageSize)
        {
            errors.Add(new FieldError("pageSize", "VALIDATION_FAILED", $"Page size must be between 1 and {PagedRequest.MaxPageSize}."));
        }

        return errors.Count > 0 ? throw new RefusalException(errors) : (p, size);
    }

    private static void SetETag(HttpResponse response, string version)
        => response.SetETag(uint.Parse(version, CultureInfo.InvariantCulture));
}
