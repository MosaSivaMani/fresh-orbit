using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Delivery.Domain.Templates.Events;

/// <summary>A slot template was created or changed; Delivery's own handler updates future slots (DEL-R12).</summary>
public sealed record SlotTemplateChanged(Guid SlotTemplateId, Guid FulfillmentLocationId, bool IsActive) : DomainEvent;
