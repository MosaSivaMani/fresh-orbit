using FreshOrbit.Delivery.Public;
using FreshOrbit.Settings.Public;

namespace FreshOrbit.Settings.Application.OrderPolicies;

/// <summary>Delivery's <see cref="IDeliveryAttemptPolicy"/>: <c>max_delivery_attempts</c> of the city's order policy (D12).</summary>
public sealed class SettingsDeliveryAttemptPolicy(ISettingsApi settings) : IDeliveryAttemptPolicy
{
    public async Task<int> GetMaxDeliveryAttemptsAsync(Guid cityId, CancellationToken cancellationToken = default)
        => (await settings.GetOrderPolicyAsync(cityId, cancellationToken)).MaxDeliveryAttempts;
}
