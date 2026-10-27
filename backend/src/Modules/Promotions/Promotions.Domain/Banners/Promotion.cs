using FreshOrbit.Promotions.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Promotions.Domain.Banners;

/// <summary>What a banner links to. Stored as CATEGORY / PRODUCT / COUPON.</summary>
public enum PromotionLinkType
{
    Category,
    Product,
    Coupon,
}

/// <summary>Everything an admin sets on a home banner.</summary>
/// <param name="Badge">Optional short label shown on the banner ("NEW", "20% OFF"); blank → none.</param>
public sealed record PromotionDefinition(
    string Title,
    string? Subtitle,
    string? ImageKey,
    PromotionLinkType LinkType,
    string LinkTarget,
    Guid? CityId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder,
    bool IsActive,
    string? Badge = null);

/// <summary>
/// A home offer banner (<c>promotions.promotions</c>). Shown when active and <c>starts_at &lt;= at &lt; ends_at</c>
/// (open-ended when <c>ends_at</c> is null), for its city or for every city when <c>city_id</c> is null.
/// </summary>
public sealed class Promotion : AggregateRoot
{
    public const int MaxTitleLength = 120;
    public const int MaxSubtitleLength = 200;
    public const int MaxBadgeLength = 20;

    private Promotion()
    {
        Title = string.Empty;
        LinkTarget = string.Empty;
    }

    public string Title { get; private set; }

    public string? Subtitle { get; private set; }

    public string? ImageKey { get; private set; }

    /// <summary>Short label the clients show on the banner ("NEW", "20% OFF"), as the admin typed it; null when none.</summary>
    public string? Badge { get; private set; }

    public PromotionLinkType LinkType { get; private set; }

    /// <summary>Category or product id (as text) or a coupon code.</summary>
    public string LinkTarget { get; private set; }

    public Guid? CityId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static Promotion Create(PromotionDefinition definition)
    {
        var promotion = new Promotion();
        promotion.Update(definition);
        return promotion;
    }

    public static IReadOnlyList<FieldError> Validate(PromotionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Trim().Length > MaxTitleLength)
        {
            errors.Add(new FieldError("title", PromotionsErrors.ValidationFailed, $"Title is required and must be at most {MaxTitleLength} characters."));
        }

        if (definition.Subtitle is { Length: > MaxSubtitleLength })
        {
            errors.Add(new FieldError("subtitle", PromotionsErrors.ValidationFailed, $"Subtitle must be at most {MaxSubtitleLength} characters."));
        }

        if (definition.Badge is not null && definition.Badge.Trim().Length > MaxBadgeLength)
        {
            errors.Add(new FieldError("badge", PromotionsErrors.ValidationFailed, $"Badge must be at most {MaxBadgeLength} characters."));
        }

        if (string.IsNullOrWhiteSpace(definition.LinkTarget))
        {
            errors.Add(new FieldError("linkTarget", PromotionsErrors.ValidationFailed, "Link target is required."));
        }
        else if (definition.LinkType is PromotionLinkType.Category or PromotionLinkType.Product && !Guid.TryParse(definition.LinkTarget, out _))
        {
            errors.Add(new FieldError("linkTarget", PromotionsErrors.ValidationFailed, "Link target must be the id of a category or product."));
        }

        if (definition.EndsAt is { } endsAt && endsAt <= definition.StartsAt)
        {
            errors.Add(new FieldError("endsAt", PromotionsErrors.ValidationFailed, "Ends at must be after starts at."));
        }

        return errors;
    }

    public void Update(PromotionDefinition definition)
    {
        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Title = definition.Title.Trim();
        Subtitle = string.IsNullOrWhiteSpace(definition.Subtitle) ? null : definition.Subtitle.Trim();
        ImageKey = string.IsNullOrWhiteSpace(definition.ImageKey) ? null : definition.ImageKey.Trim();
        Badge = string.IsNullOrWhiteSpace(definition.Badge) ? null : definition.Badge.Trim();
        LinkType = definition.LinkType;
        LinkTarget = definition.LinkType == PromotionLinkType.Coupon ? definition.LinkTarget.Trim().ToUpperInvariant() : definition.LinkTarget.Trim();
        CityId = definition.CityId;
        StartsAt = definition.StartsAt;
        EndsAt = definition.EndsAt;
        SortOrder = definition.SortOrder;
        IsActive = definition.IsActive;
    }

    public bool IsShownAt(DateTimeOffset at, Guid? cityId)
        => IsActive && StartsAt <= at && (EndsAt is null || at < EndsAt) && (CityId is null || CityId == cityId);
}
