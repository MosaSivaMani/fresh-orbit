using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Pricing.Domain.Periods.Events;

public sealed record PriceValues(long UnitPricePaise, long? CompareAtPaise);

public sealed record VariantPriceScheduled(Guid FulfillmentLocationId, Guid VariantId, PriceValues? Previous, PriceValues New, DateTimeOffset ValidFrom) : DomainEvent;

public sealed record VariantPriceEnded(Guid FulfillmentLocationId, Guid VariantId, DateTimeOffset EndedAt) : DomainEvent;

public sealed record PreparationChargeScheduled(Guid FulfillmentLocationId, Guid VariantId, string PreparationCode, long ChargePaise, ChargeBasis ChargeBasis, DateTimeOffset ValidFrom) : DomainEvent;

public sealed record TaxRuleScheduled(string TaxCategoryCode, int RateBp, DateTimeOffset ValidFrom) : DomainEvent;

/// <param name="ChangedKeys">"PRICE:{locationCode}:{sku}" and "PREP_CHARGE:{locationCode}:{sku}:{preparationCode}" per written row.</param>
public sealed record BulkPricesApplied(Guid UploadId, int RowCount, IReadOnlyList<string> ChangedKeys) : DomainEvent;
