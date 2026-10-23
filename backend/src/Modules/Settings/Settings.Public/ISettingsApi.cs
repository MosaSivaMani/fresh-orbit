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

    Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default);
}

/// <param name="MaxOverweightBp">Allowed packed overweight in basis points (1000 = +10 %).</param>
/// <param name="CancelAllowedUntilStatus">Order status code up to which a customer may cancel (e.g. CONFIRMED).</param>
public sealed record OrderPolicy(int ReservationMinutes, int MaxOverweightBp, string CancelAllowedUntilStatus, long CodMaxOrderPaise);

public sealed record BusinessProfile(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail);
