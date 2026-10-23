using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.OrderPolicies;

namespace FreshOrbit.Settings.Domain.Shared;

/// <summary>Loads and adds Settings aggregates on the shared unit of work.</summary>
public interface ISettingsRepository
{
    Task<BusinessProfile?> GetBusinessProfileAsync(CancellationToken cancellationToken);

    Task<OrderPolicy?> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderPolicy>> ListOrderPoliciesAsync(CancellationToken cancellationToken);

    Task<NumberSeries?> GetNumberSeriesAsync(string code, string period, CancellationToken cancellationToken);

    Task<IReadOnlyList<NumberSeries>> ListNumberSeriesAsync(CancellationToken cancellationToken);

    /// <summary>Loads the series row with <c>SELECT … FOR UPDATE</c> (held until the caller's transaction ends).</summary>
    Task<NumberSeries?> LockNumberSeriesAsync(string code, string period, CancellationToken cancellationToken);

    void Add(BusinessProfile profile);

    void Add(OrderPolicy policy);

    void Add(NumberSeries series);
}
