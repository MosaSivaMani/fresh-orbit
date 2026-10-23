using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Locations.Public;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Delivery.Tests.Support;

/// <summary>The real Api with <see cref="FakeLocations"/> standing in for the Locations module.</summary>
public sealed class DeliveryApiFactory(DeliveryDatabase database) : ApiFactory(database)
{
    public FakeLocations Locations { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILocationsApi>(Locations));
        services.Replace(ServiceDescriptor.Singleton<ILocationCalendar>(Locations));
    }
}
