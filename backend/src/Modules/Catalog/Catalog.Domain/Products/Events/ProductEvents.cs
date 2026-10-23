using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Catalog.Domain.Products.Events;

public sealed record ProductPublished(Guid ProductId, string Slug) : DomainEvent;

public sealed record ProductArchived(Guid ProductId) : DomainEvent;

/// <param name="ChangedFields">Names of the changed request fields (<c>created</c> for a new product).</param>
public sealed record ProductUpdated(Guid ProductId, IReadOnlyList<string> ChangedFields) : DomainEvent;

public sealed record VariantActivated(Guid VariantId, Guid ProductId) : DomainEvent;

public sealed record VariantDeactivated(Guid VariantId, Guid ProductId) : DomainEvent;

public sealed record PreparationYield(string Code, int YieldBp, bool IsDefault);

public sealed record VariantPreparationsChanged(Guid VariantId, Guid ProductId, IReadOnlyList<PreparationYield> Preparations) : DomainEvent;

/// <param name="StorageKey">Key prefix of the image's files (<c>products/{productId}/{imageId}</c>).</param>
public sealed record ProductImageRemoved(Guid ProductId, Guid ImageId, string StorageKey) : DomainEvent;
