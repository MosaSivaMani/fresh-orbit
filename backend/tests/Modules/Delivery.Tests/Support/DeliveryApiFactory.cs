using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Delivery.Tests.Support;

/// <summary>
/// The real Api with <see cref="FakeLocations"/> standing in for the Locations module and a fixed order policy
/// (<see cref="MaxDeliveryAttempts"/>) standing in for Settings.
/// </summary>
public sealed class DeliveryApiFactory(DeliveryDatabase database) : ApiFactory(database)
{
    public const int MaxDeliveryAttempts = 3;

    public FakeLocations Locations { get; } = new();

    /// <summary>The order facts the shipment reads show (AD-1, AD-2); an order not added reads as absent.</summary>
    public FakeOrdering Ordering { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILocationsApi>(Locations));
        services.Replace(ServiceDescriptor.Singleton<ILocationCalendar>(Locations));
        services.Replace(ServiceDescriptor.Singleton<IOrderingApi>(Ordering));
        services.RemoveAll<IDeliveryAttemptPolicy>();
        services.AddSingleton<IDeliveryAttemptPolicy>(new FixedAttemptPolicy(MaxDeliveryAttempts));
    }

    private sealed class FixedAttemptPolicy(int maxAttempts) : IDeliveryAttemptPolicy
    {
        public Task<int> GetMaxDeliveryAttemptsAsync(Guid cityId, CancellationToken cancellationToken = default) => Task.FromResult(maxAttempts);
    }
}
