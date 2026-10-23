using FreshOrbit.Pricing.Public;

namespace FreshOrbit.Cart.Application.Abstractions;

/// <summary><see cref="IDeliveryFeeTaxRate"/> over <c>IPricingApi.GetTaxRatesAsync</c> (tax category DELIVERY_SERVICE).</summary>
public sealed class PricingDeliveryFeeTaxRate(IPricingApi pricing) : IDeliveryFeeTaxRate
{
    public const string DeliveryServiceTaxCategory = "DELIVERY_SERVICE";

    public async Task<int?> GetRateBpAsync(DateTimeOffset at, CancellationToken cancellationToken)
    {
        var rates = await pricing.GetTaxRatesAsync([DeliveryServiceTaxCategory], at, cancellationToken);
        return rates.TryGetValue(DeliveryServiceTaxCategory, out var rate) ? rate : null;
    }
}
