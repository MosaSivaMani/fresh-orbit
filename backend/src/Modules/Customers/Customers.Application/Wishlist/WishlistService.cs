using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Domain.Wishlist;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Wishlist;

/// <summary>
/// Product ids only: Catalog.Public offers no product lookup yet, so names, images and prices (with LOC) are left to
/// the client's catalog calls (see report).
/// </summary>
public sealed record WishlistItemResponse(Guid ProductId, DateTimeOffset AddedAt);

public interface IWishlistService
{
    Task<IReadOnlyList<WishlistItemResponse>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Guid productId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid productId, CancellationToken cancellationToken);
}

/// <summary>CUS-R6: add is idempotent, removing a missing item succeeds.</summary>
public sealed class WishlistService(CustomerContext context, ICustomersRepository repository, IUnitOfWork unitOfWork) : IWishlistService
{
    public async Task<IReadOnlyList<WishlistItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var items = await repository.GetWishlistAsync(customer.Id, cancellationToken);
        return [.. items.OrderByDescending(i => i.AddedAt).Select(i => new WishlistItemResponse(i.ProductId, i.AddedAt))];
    }

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
