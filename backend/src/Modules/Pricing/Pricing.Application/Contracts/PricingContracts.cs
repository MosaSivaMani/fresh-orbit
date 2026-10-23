using FreshOrbit.Pricing.Domain.Periods;

namespace FreshOrbit.Pricing.Application.Contracts;

/// <summary>Version precondition sent with a schedule write: <c>If-Match</c> version, or <c>If-None-Match: *</c> when no period exists yet.</summary>
public sealed record SchedulePrecondition(uint? IfMatch, bool IfNoneMatchAny);

/// <param name="ValidFrom">Omitted = now (PRC-R3).</param>
/// <param name="ReplaceFuture">Replace FUTURE periods that start after <paramref name="ValidFrom"/> (PRC-R4).</param>
public sealed record SetPriceRequest(long UnitPricePaise, long? CompareAtPaise, DateTimeOffset? ValidFrom, string? Reason, bool? ReplaceFuture);

public sealed record PricePeriodResponse(Guid Id, long UnitPricePaise, long? CompareAtPaise, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

/// <param name="Version">Version of the schedule (latest period row); null when there is none — send <c>If-None-Match: *</c>.</param>
/// <param name="Unchanged">True when the request matched the CURRENT price and nothing was written (PRC-R5).</param>
/// <param name="LargeChange">More than 50 % up or down against the previous price (PRC-R14).</param>
public sealed record PriceScheduleResponse(
    Guid FulfillmentLocationId,
    Guid VariantId,
    string? Version,
    PricePeriodResponse? Current,
    PricePeriodResponse? Next,
    IReadOnlyList<PricePeriodResponse> History,
    bool Unchanged,
    bool LargeChange);

public sealed record SetChargeRequest(long ChargePaise, ChargeBasis ChargeBasis, DateTimeOffset? ValidFrom, bool? ReplaceFuture);

public sealed record ChargePeriodResponse(Guid Id, long ChargePaise, ChargeBasis ChargeBasis, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

public sealed record ChargeScheduleResponse(
    Guid FulfillmentLocationId,
    Guid VariantId,
    string PreparationCode,
    string? Version,
    ChargePeriodResponse? Current,
    ChargePeriodResponse? Next,
    IReadOnlyList<ChargePeriodResponse> History,
    bool Unchanged);

public sealed record ScheduleTaxRuleRequest(string? TaxCategoryCode, int RateBp, DateTimeOffset? ValidFrom, bool? ReplaceFuture);

public sealed record TaxRulePeriodResponse(Guid Id, int RateBp, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

public sealed record TaxCategoryRulesResponse(
    string TaxCategoryCode,
    string Name,
    string? Version,
    TaxRulePeriodResponse? Current,
    TaxRulePeriodResponse? Next,
    IReadOnlyList<TaxRulePeriodResponse> History,
    bool Unchanged);

/// <param name="Missing">No CURRENT price at the location (not for sale there, PRC-R1).</param>
public sealed record PriceListItem(
    Guid FulfillmentLocationId,
    Guid VariantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string VariantName,
    string PriceUnit,
    PricePeriodResponse? Current,
    PricePeriodResponse? Next,
    bool Missing);

public enum BulkRowKind
{
    Price,
    PrepCharge,
}

/// <summary>One row of a bulk upload (PRC-R13); PRICE rows use the price fields, PREP_CHARGE rows the charge fields.</summary>
public sealed record BulkPriceRow(
    BulkRowKind? Kind,
    string? LocationCode,
    string? Sku,
    long? UnitPricePaise,
    long? CompareAtPaise,
    string? PreparationCode,
    long? ChargePaise,
    ChargeBasis? ChargeBasis,
    DateTimeOffset? ValidFrom);

public sealed record BulkPriceRequest(DateTimeOffset? ValidFrom, bool? ReplaceFuture, IReadOnlyList<BulkPriceRow?>? Rows);

public enum BulkRowStatus
{
    Changed,
    Unchanged,
}

public sealed record BulkValues(long? UnitPricePaise, long? CompareAtPaise, long? ChargePaise, ChargeBasis? ChargeBasis);

/// <param name="ChangeBp">Change of the price or charge against the current value, in basis points; null without a current value.</param>
public sealed record BulkRowResult(
    int Index,
    BulkRowKind Kind,
    string LocationCode,
    string Sku,
    string? PreparationCode,
    BulkRowStatus Status,
    BulkValues? Current,
    BulkValues New,
    DateTimeOffset ValidFrom,
    int? ChangeBp,
    bool LargeChange);

public sealed record BulkPriceResponse(Guid? UploadId, bool DryRun, int RowCount, int ChangedCount, int UnchangedCount, IReadOnlyList<BulkRowResult> Rows);
