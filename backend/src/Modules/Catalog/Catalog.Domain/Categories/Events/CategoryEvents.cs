using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Catalog.Domain.Categories.Events;

public sealed record CategoryChanged(Guid CategoryId, bool IsActive) : DomainEvent;
