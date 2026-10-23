using FreshOrbit.Settings.Application.Numbering;
using FreshOrbit.Settings.Application.OrderPolicies;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using DomainBusinessProfile = FreshOrbit.Settings.Domain.BusinessProfiles.BusinessProfile;
using DomainOrderPolicy = FreshOrbit.Settings.Domain.OrderPolicies.OrderPolicy;

namespace FreshOrbit.Settings.Infrastructure.Public;

internal sealed class SettingsApi(FreshOrbitDbContext db, INumberSeriesService numberSeries) : ISettingsApi
{
    public async Task<OrderPolicy> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken = default)
    {
        var rows = await db.Set<DomainOrderPolicy>().AsNoTracking()
            .Where(p => p.CityId == null || (cityId != null && p.CityId == cityId))
            .ToListAsync(cancellationToken);
        var policy = rows.FirstOrDefault(p => cityId is not null && p.CityId == cityId) ?? rows.FirstOrDefault(p => p.CityId is null)
            ?? throw OrderPolicyService.Missing();
        return new OrderPolicy(policy.ReservationMinutes, policy.MaxOverweightBp, policy.CancelAllowedUntilStatus, policy.CodMaxOrderPaise)
        {
            PolicyId = policy.Id,
            CityId = policy.CityId,
            EnabledPaymentMethods = [.. policy.EnabledPaymentMethods],
            SubstitutionResponseMinutes = policy.SubstitutionResponseMinutes,
            ComplaintWindowHours = policy.ComplaintWindowHours,
            MaxDeliveryAttempts = policy.MaxDeliveryAttempts,
        };
    }

    public Task<string> NextNumberAsync(string seriesCode, string period, CancellationToken cancellationToken = default)
        => numberSeries.NextNumberAsync(seriesCode, period, cancellationToken);

    public async Task<string> NextNumberAsync(string seriesCode, DateTimeOffset at, CancellationToken cancellationToken = default)
        => await numberSeries.NextNumberAsync(seriesCode, await numberSeries.PeriodForAsync(at, cancellationToken), cancellationToken);

    public Task<string> GetFinancialPeriodAsync(DateTimeOffset at, CancellationToken cancellationToken = default)
        => numberSeries.PeriodForAsync(at, cancellationToken);

    public async Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default)
    {
        var profile = await db.Set<DomainBusinessProfile>().AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(SettingsErrors.ConfigurationMissing, "The business profile has not been entered; an admin must save it in settings.");
        return new BusinessProfile(profile.LegalName, profile.TradeName, profile.Gstin, profile.FssaiLicenseNo, profile.Address, profile.SupportPhone, profile.SupportEmail)
        {
            TimeZone = profile.TimeZone,
        };
    }
}
