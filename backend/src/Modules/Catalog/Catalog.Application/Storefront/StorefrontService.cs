using System.Text.RegularExpressions;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Catalog.Application.Storefront;

/// <summary>Location context and request validation of the customer browse endpoints.</summary>
public sealed partial class StorefrontService(IStorefrontReads reads)
{
    public const int QueryMinLength = 2;
    public const int QueryMaxLength = 60;

    /// <summary>
    /// Resolves the pincode (docs/api §1.4). Absent: null, or 422 LOCATION_REQUIRED when <paramref name="required"/>.
    /// Unknown, inactive or malformed: 422 PINCODE_NOT_SERVICEABLE.
    /// </summary>
    public async Task<StorefrontLocation?> ResolveLocationAsync(string? pincode, bool required, CancellationToken cancellationToken)
    {
        var value = pincode?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return required ? throw LocationRequired("pincode") : null;
        }

        if (!PincodePattern().IsMatch(value))
        {
            throw new DomainException(CatalogErrors.PincodeNotServiceable, "We do not deliver to this pincode.", "pincode");
        }

        return await reads.ResolveLocationAsync(value, cancellationToken)
            ?? throw new DomainException(CatalogErrors.PincodeNotServiceable, "We do not deliver to this pincode.", "pincode");
    }

    /// <summary>Validates <c>GET /products</c> parameters (CAT-R12, CAT-R14, CAT-R15).</summary>
    public static ProductListQuery BuildQuery(
        IReadOnlyList<string>? categories,
        string? q,
        IReadOnlyList<string>? sourceTypes,
        IReadOnlyList<string>? preparations,
        bool? inStock,
        long? minPricePaise,
        long? maxPricePaise,
        string? sort,
        int skip,
        int take,
        bool hasLocation)
    {
        var errors = new List<FieldError>();
        var query = NormalizeQuery(q, errors);
        var types = new List<SourceType>();
        foreach (var code in sourceTypes ?? [])
        {
            if (Enum.TryParse<SourceType>(code, ignoreCase: true, out var type) && Enum.IsDefined(type))
            {
                types.Add(type);
            }
            else
            {
                errors.Add(new FieldError("sourceType", CatalogErrors.ValidationFailed, "Use WILD or FARMED."));
            }
        }

        if (minPricePaise is < 0 || maxPricePaise is < 0 || (minPricePaise is { } min && maxPricePaise is { } max && max < min))
        {
            errors.Add(new FieldError("minPricePaise", CatalogErrors.ValidationFailed, "Enter a price range with 0 ≤ minPricePaise ≤ maxPricePaise."));
        }

        var parsedSort = ParseSort(sort, query is not null, errors);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        if (!hasLocation)
        {
            if (minPricePaise is not null || maxPricePaise is not null)
            {
                throw LocationRequired("minPricePaise");
            }

            if (inStock is not null)
            {
                throw LocationRequired("inStock");
            }

            if (parsedSort is ProductSort.Price or ProductSort.PriceDescending)
            {
                throw LocationRequired("sort");
            }
        }

        return new ProductListQuery(
            [.. (categories ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())],
            query,
            types,
            [.. (preparations ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim().ToUpperInvariant())],
            inStock,
            minPricePaise,
            maxPricePaise,
            parsedSort,
            skip,
            take);
    }

    /// <summary>Validates the suggest query (2–60 characters after trim).</summary>
    public static string BuildSuggestQuery(string? q)
    {
        var errors = new List<FieldError>();
        var query = NormalizeQuery(q, errors);
        if (query is null && errors.Count == 0)
        {
            errors.Add(new FieldError("q", CatalogErrors.ValidationFailed, $"Enter {QueryMinLength} to {QueryMaxLength} characters to search."));
        }

        return errors.Count > 0 ? throw new RefusalException(errors) : query!;
    }

    /// <summary>The search terms of <paramref name="q"/>: runs of letters and digits.</summary>
    public static IReadOnlyList<string> Tokens(string q) => [.. TokenPattern().Matches(q).Select(m => m.Value)];

    public static DomainException LocationRequired(string field)
        => new(CatalogErrors.LocationRequired, "Send a serviceable pincode (X-Pincode header or pincode query) for prices and availability.", field);

    private static string? NormalizeQuery(string? q, List<FieldError> errors)
    {
        if (q is null)
        {
            return null;
        }

        var trimmed = q.Trim();
        if (trimmed.Length is < QueryMinLength or > QueryMaxLength || Tokens(trimmed).Count == 0)
        {
            errors.Add(new FieldError("q", CatalogErrors.ValidationFailed, $"Enter {QueryMinLength} to {QueryMaxLength} characters to search."));
            return null;
        }

        return trimmed;
    }

    private static ProductSort ParseSort(string? sort, bool hasQuery, List<FieldError> errors)
    {
        switch (sort?.Trim())
        {
            case null or "":
                return hasQuery ? ProductSort.Relevance : ProductSort.Default;
            case "relevance" when hasQuery:
                return ProductSort.Relevance;
            case "relevance":
                errors.Add(new FieldError("sort", CatalogErrors.ValidationFailed, "Sort by relevance needs a search query q."));
                return ProductSort.Default;
            case "price":
                return ProductSort.Price;
            case "-price":
                return ProductSort.PriceDescending;
            case "name":
                return ProductSort.Name;
            case "-newest":
                return ProductSort.Newest;
            default:
                errors.Add(new FieldError("sort", CatalogErrors.ValidationFailed, "Sort by relevance, price, -price, name or -newest."));
                return ProductSort.Default;
        }
    }

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PincodePattern();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex TokenPattern();
}

/// <summary>Availability of stock at a location (CAT-R10, docs/api §4.4).</summary>
public static class StockMath
{
    public const string LowStockMessage = "Only a few left today.";
    public const string OutOfStockMessage = "Out of stock today.";
    public const string NotSoldHereMessage = "Not sold at your location.";

    public static AvailabilityStatus Status(int available, int lowStockThreshold)
        => available <= 0 ? AvailabilityStatus.OutOfStock : available <= lowStockThreshold ? AvailabilityStatus.LowStock : AvailabilityStatus.InStock;

    public static string? Message(AvailabilityStatus status) => status switch
    {
        AvailabilityStatus.LowStock => LowStockMessage,
        AvailabilityStatus.OutOfStock => OutOfStockMessage,
        AvailabilityStatus.NotSoldHere => NotSoldHereMessage,
        _ => null,
    };

    /// <summary>
    /// Largest valid quantity (in the variant's quantity unit) that <paramref name="availableStock"/> covers: KG variants
    /// convert gross grams to net with the yield (net = floor(gross × yield / 10000), the inverse of CAT-R6); the result
    /// is capped at maxOrder, floored to orderStep, and 0 when below minOrder.
    /// </summary>
    public static int MaxOrderable(int availableStock, SellUnit sellUnit, int yieldBp, int minOrder, int maxOrder, int orderStep)
    {
        if (availableStock <= 0 || orderStep <= 0)
        {
            return 0;
        }

        var coverable = sellUnit == SellUnit.Kg ? (int)((long)availableStock * yieldBp / BasisPoints.Whole) : availableStock;
        var quantity = Math.Min(coverable, maxOrder);
        quantity -= quantity % orderStep;
        return quantity < minOrder ? 0 : quantity;
    }
}
