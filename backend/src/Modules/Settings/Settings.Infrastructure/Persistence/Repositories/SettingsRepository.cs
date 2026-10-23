using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.OrderPolicies;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Settings.Infrastructure.Persistence.Repositories;

internal sealed class SettingsRepository(FreshOrbitDbContext db) : ISettingsRepository
{
    public Task<BusinessProfile?> GetBusinessProfileAsync(CancellationToken cancellationToken)
        => db.Set<BusinessProfile>().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<OrderPolicy?> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken)
        => db.Set<OrderPolicy>().FirstOrDefaultAsync(p => p.CityId == cityId, cancellationToken);

    public async Task<IReadOnlyList<OrderPolicy>> ListOrderPoliciesAsync(CancellationToken cancellationToken)
        => await db.Set<OrderPolicy>().ToListAsync(cancellationToken);

    public Task<NumberSeries?> GetNumberSeriesAsync(string code, string period, CancellationToken cancellationToken)
        => db.Set<NumberSeries>().FirstOrDefaultAsync(s => s.Code == code && s.Period == period, cancellationToken);

    public async Task<IReadOnlyList<NumberSeries>> ListNumberSeriesAsync(CancellationToken cancellationToken)
        => await db.Set<NumberSeries>().ToListAsync(cancellationToken);

    public async Task<NumberSeries?> LockNumberSeriesAsync(string code, string period, CancellationToken cancellationToken)
    {
        // Not composed further, so EF sends the statement as written; xmin is selected for the row version.
        var rows = await db.Set<NumberSeries>()
            .FromSql($"SELECT n.*, n.xmin FROM settings.number_series n WHERE n.code = {code} AND n.period = {period} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.FirstOrDefault();
    }

    public void Add(BusinessProfile profile) => db.Add(profile);

    public void Add(OrderPolicy policy) => db.Add(policy);

    public void Add(NumberSeries series) => db.Add(series);
}
