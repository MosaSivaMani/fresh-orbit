using FreshOrbit.Catalog.Public;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Domain.Wishlist;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Wishlist;

/// <summary>A wishlist entry (W5). <paramref name="Product"/> is null when the product is no longer on the storefront.</summary>
public sealed record WishlistItemResponse(Guid ProductId, DateTimeOffset AddedAt, WishlistProductView? Product = null);

/// <param name="ImageUrl">Primary image (small rendition); null without an image.</param>
/// <param name="Price">Server price at the location context (lowest stocked variant, as on product cards); null without a location or when not sold there.</param>
/// <param name="IsAvailable">Sold at the location and in stock now; false without a location.</param>
public sealed record WishlistProductView(string Slug, string Name, string? ImageUrl, WishlistPriceView? Price, bool IsAvailable);

/// <param name="PerUnit">KG, PIECE or PACK.</param>
public sealed record WishlistPriceView(long UnitPricePaise, long? CompareAtPaise, string PerUnit);

public interface IWishlistService
{
    /// <summary>Newest first; priced at <paramref name="pincode"/> when given (422 PINCODE_NOT_SERVICEABLE when not served).</summary>
    Task<IReadOnlyList<WishlistItemResponse>> ListAsync(string? pincode, CancellationToken cancellationToken);

    Task AddAsync(Guid productId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid productId, CancellationToken cancellationToken);
}

/// <summary>CUS-R6: add is idempotent, removing a missing item succeeds.</summary>
public sealed class WishlistService(CustomerContext context, ICustomersRepository repository, IUnitOfWork unitOfWork, ICatalogApi catalog, ILocationsApi locations) : IWishlistService
{
    public const string PincodeNotServiceable = "PINCODE_NOT_SERVICEABLE";

    /// <summary>Read: customer, items, location; then one batched Catalog read for every card (no call per item).</summary>
    public async Task<IReadOnlyList<WishlistItemResponse>> ListAsync(string? pincode, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        Guid? locationId = null;
        if (!string.IsNullOrWhiteSpace(pincode))
        {
            var served = await locations.ResolvePincodeAsync(pincode.Trim(), cancellationToken);
            locationId = served is { IsServiceable: true, FulfillmentLocationId: { } id }
                ? id
                : throw new DomainException(PincodeNotServiceable, "We do not deliver to this pincode yet; enter another pincode.", "pincode");
        }

        var items = await repository.GetWishlistAsync(customer.Id, cancellationToken);
        if (items.Count == 0)
        {
            return [];
        }

        var cards = (await catalog.GetProductCardsAsync([.. items.Select(i => i.ProductId)], locationId, context.Now, cancellationToken))
            .ToDictionary(c => c.ProductId);
        return [.. items.OrderByDescending(i => i.AddedAt).Select(i => new WishlistItemResponse(i.ProductId, i.AddedAt, View(cards.GetValueOrDefault(i.ProductId))))];
    }

    private static WishlistProductView? View(ProductCard? card)
        => card is not { IsVisible: true }
            ? null
            : new WishlistProductView(
                card.Slug,
                card.Name,
                card.ImageUrl,
                card.Price is { } price ? new WishlistPriceView(price.UnitPricePaise, price.CompareAtPaise, price.PerUnit.ToCode()) : null,
                card.IsAvailable);

    public async Task AddAsync(Guid productId, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        if (await repository.GetWishlistItemAsync(customer.Id, productId, cancellationToken) is not null)
        {
            return;
        }

        repository.Add(WishlistItem.Create(customer.Id, productId, context.Now));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (repository.IsDuplicateKey(exception))
        {
            // A parallel PUT inserted the same item first: the outcome is the same.
        }
    }

    public async Task RemoveAsync(Guid productId, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var item = await repository.GetWishlistItemAsync(customer.Id, productId, cancellationToken);
        if (item is null)
        {
            return;
        }

        repository.Remove(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
