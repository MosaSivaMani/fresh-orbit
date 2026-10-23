namespace FreshOrbit.Settings.Public;

/// <summary>What other modules may ask the Settings module.</summary>
public interface ISettingsApi
{
    /// <summary>The city's order policy, or the default one when the city has none. Missing configuration is refused.</summary>
    Task<OrderPolicy> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless number of a series (e.g. ORDER, INVOICE) for a period. Locks and increments the series row in the
    /// caller's transaction (tracked; saved by the caller's SaveChanges).
    /// </summary>
    Task<string> NextNumberAsync(string seriesCode, string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// As <see cref="NextNumberAsync(string, string, CancellationToken)"/> with the period being the Indian financial
    /// year that contains <paramref name="at"/> in the business time zone.
    /// </summary>
    Task<string> NextNumberAsync(string seriesCode, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>The financial-year label (<c>2026-27</c>) that contains <paramref name="at"/> in the business time zone.</summary>
    Task<string> GetFinancialPeriodAsync(DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default);
}

/// <param name="MaxOverweightBp">Allowed packed overweight in basis points (1000 = +10 %).</param>
/// <param name="CancelAllowedUntilStatus">Order status code up to which a customer may cancel (e.g. CONFIRMED).</param>
public sealed record OrderPolicy(int ReservationMinutes, int MaxOverweightBp, string CancelAllowedUntilStatus, long CodMaxOrderPaise)
{
    /// <summary>Policy row id.</summary>
    public Guid PolicyId { get; init; }

    /// <summary>The city the row belongs to; null for the default row.</summary>
    public Guid? CityId { get; init; }

    /// <summary>Payment method codes customers may choose (COD, UPI_ON_DELIVERY, ONLINE).</summary>
    public IReadOnlyList<string> EnabledPaymentMethods { get; init; } = [];

    /// <summary>Minutes a customer has to answer a substitution proposal.</summary>
    public int SubstitutionResponseMinutes { get; init; }

    /// <summary>Hours after delivery during which a complaint may be raised.</summary>
    public int ComplaintWindowHours { get; init; }

    /// <summary>Delivery attempts allowed on the same slot date (D12).</summary>
    public int MaxDeliveryAttempts { get; init; }
}

public sealed record BusinessProfile(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail)
{
    /// <summary>IANA time zone of the business (financial-year boundary, invoice dates).</summary>
    public string TimeZone { get; init; } = string.Empty;
}
