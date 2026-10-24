using FreshOrbit.Catalog.Public;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Domain.StockAlerts;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.StockAlerts;

/// <summary>Body of <c>POST /me/stock-alerts</c>.</summary>
public sealed record CreateStockAlertRequest(Guid VariantId, Guid FulfillmentLocationId);

public sealed record StockAlertResponse(Guid Id, Guid VariantId, Guid FulfillmentLocationId, DateTimeOffset CreatedAt);

public interface IStockAlertService
{
    Task<IReadOnlyList<StockAlertResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates the alert, or returns the existing one for the same variant and location (<c>Created</c> false).</summary>
    Task<(StockAlertResponse Alert, bool Created)> AddAsync(CreateStockAlertRequest request, CancellationToken cancellationToken);

    /// <summary>Removes the caller's alert; an unknown id (or another customer's) is not an error.</summary>
    Task RemoveAsync(Guid alertId, CancellationToken cancellationToken);
}

/// <summary>
/// CUS-R9 back-in-stock alerts. Read (customer, alerts, variant, location, stock) → Decide → one save. The alert is
/// refused for a variant that is not sold, a location that does not exist, or stock that is available now (the alert
/// could never fire until it ran out again).
/// </summary>
public sealed class StockAlertService(
    CustomerContext context,
    ICustomersRepository repository,
    IUnitOfWork unitOfWork,
    ICatalogApi catalog,
    ILocationsApi locations,
    IInventoryApi inventory) : IStockAlertService
{
    public const string StockAvailable = "STOCK_AVAILABLE";
    public const string ProductNotAvailable = "PRODUCT_NOT_AVAILABLE";
    public const string LimitReached = "STOCK_ALERT_LIMIT_REACHED";
    public const string ValidationFailed = "VALIDATION_FAILED";

    public async Task<IReadOnlyList<StockAlertResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        return [.. (await repository.GetStockAlertsAsync(customer.Id, cancellationToken)).Select(ToResponse)];
    }

    public async Task<(StockAlertResponse Alert, bool Created)> AddAsync(CreateStockAlertRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<FieldError>();
        if (request.VariantId == Guid.Empty)
        {
            errors.Add(new FieldError("variantId", ValidationFailed, "Enter the variantId of the product variant to watch."));
        }

        if (request.FulfillmentLocationId == Guid.Empty)
        {
            errors.Add(new FieldError("fulfillmentLocationId", ValidationFailed, "Enter the fulfillmentLocationId the product should come back at."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        // Read.
        var customer = await context.LoadAsync(cancellationToken);
        var alerts = await repository.GetStockAlertsAsync(customer.Id, cancellationToken);
        var existing = alerts.FirstOrDefault(a => a.VariantId == request.VariantId && a.FulfillmentLocationId == request.FulfillmentLocationId);
        if (existing is not null)
        {
            return (ToResponse(existing), false);
        }

        var variants = await catalog.GetVariantsAsync([request.VariantId], cancellationToken);
        var location = await locations.GetFulfillmentLocationsAsync([request.FulfillmentLocationId], cancellationToken);
        var stock = await inventory.GetAvailabilityAsync(request.FulfillmentLocationId, [request.VariantId], cancellationToken);

        // Decide.
        if (variants is not [{ IsActive: true, ProductStatus: ProductStatus.Active }])
        {
            throw new DomainException(ProductNotAvailable, "This product is not sold; choose another variantId.", "variantId");
        }

        if (location.Count == 0)
        {
            throw new RefusalException(new FieldError("fulfillmentLocationId", ValidationFailed, "No fulfillment location has this fulfillmentLocationId."));
        }

        if (stock is [{ AvailableQuantity: > 0 }])
        {
            throw new DomainException(StockAvailable, "This product is in stock now; add it to the cart instead.", "variantId");
        }

        if (alerts.Count >= StockAlert.MaxPerCustomer)
        {
            throw new DomainException(LimitReached, $"You can keep at most {StockAlert.MaxPerCustomer} stock alerts; remove one first.", "variantId");
        }

        // Commit.
        var alert = StockAlert.Create(customer.Id, request.VariantId, request.FulfillmentLocationId, context.Now);
        repository.Add(alert);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return (ToResponse(alert), true);
        }
        catch (Exception exception) when (repository.IsDuplicateKey(exception))
        {
            // A parallel POST created the same alert first: the outcome is the same.
            repository.Remove(alert);
            var winner = (await repository.GetStockAlertsAsync(customer.Id, cancellationToken))
                .First(a => a.VariantId == request.VariantId && a.FulfillmentLocationId == request.FulfillmentLocationId);
            return (ToResponse(winner), false);
        }
    }

    public async Task RemoveAsync(Guid alertId, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var alert = (await repository.GetStockAlertsAsync(customer.Id, cancellationToken)).FirstOrDefault(a => a.Id == alertId);
        if (alert is null)
        {
            return;
        }

        repository.Remove(alert);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static StockAlertResponse ToResponse(StockAlert alert) => new(alert.Id, alert.VariantId, alert.FulfillmentLocationId, alert.CreatedAt);
}

/// <summary>
/// Fires every alert waiting for a variant at a location when Inventory reports it back in stock: one read of the
/// alerts, one Catalog read for the names, then each alert raises <c>StockAlertTriggered</c> and is removed. Tracks
/// only; the outbox dispatcher saves with the inbox row.
/// </summary>
public sealed class StockAlertTrigger(ICustomersRepository repository, ICatalogApi catalog)
{
    public async Task TriggerAsync(Guid variantId, Guid fulfillmentLocationId, CancellationToken cancellationToken)
    {
        var alerts = await repository.GetStockAlertsForAsync(variantId, fulfillmentLocationId, cancellationToken);
        if (alerts.Count == 0)
        {
            return;
        }

        var variants = await catalog.GetVariantsAsync([variantId], cancellationToken);
        var variant = variants.Count > 0 ? variants[0] : null;
        foreach (var alert in alerts)
        {
            alert.Trigger(variant?.ProductName ?? string.Empty, variant?.ProductSlug ?? string.Empty, variant?.VariantName ?? string.Empty);
            repository.Remove(alert);
        }
    }
}
