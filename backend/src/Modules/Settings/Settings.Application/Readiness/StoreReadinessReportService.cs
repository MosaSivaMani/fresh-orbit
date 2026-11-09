using FreshOrbit.Locations.Public;
using FreshOrbit.Notifications.Public;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.Settings.Application.Readiness;

/// <summary>
/// <c>GET /api/v1/admin/store-readiness</c> (D358, SET-R10): what still blocks real trading. Every check is
/// <c>OK</c> or <c>MISSING</c>; the store is ready when every check is OK and at least one location exists.
/// </summary>
public sealed record StoreReadinessResponse(
    bool IsReady,
    IReadOnlyList<ProfileFieldReadiness> BusinessProfile,
    IReadOnlyList<LocationReadiness> Locations,
    IReadOnlyList<ChannelReadiness> Channels);

/// <param name="Field">camelCase business-profile field: <c>legalName</c>, <c>address</c>, <c>gstin</c> or <c>timeZone</c>.</param>
/// <param name="Status"><c>OK</c> or <c>MISSING</c>.</param>
public sealed record ProfileFieldReadiness(string Field, string Status);

/// <param name="LocationStatus">DRAFT, ACTIVE or PAUSED.</param>
/// <param name="FssaiLicenseStatus"><c>OK</c> when the premises' FSSAI licence number is entered, else <c>MISSING</c>.</param>
public sealed record LocationReadiness(Guid LocationId, Guid CityId, string Code, string Name, string LocationStatus, string FssaiLicenseStatus);

/// <param name="Channel"><c>SMS</c>, <c>EMAIL</c> or <c>PUSH</c>.</param>
/// <param name="Provider">Provider code in force (<c>DLT</c>, <c>SMTP</c>, <c>FCM</c>, <c>DISABLED</c> or <c>DEVELOPMENT</c>); never a setting's value.</param>
/// <param name="Status"><c>OK</c> when a real provider delivers, else <c>MISSING</c>.</param>
public sealed record ChannelReadiness(string Channel, string Provider, string Status);

public interface IStoreReadinessReportService
{
    Task<StoreReadinessResponse> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Read-only composition (D358): the business profile from this module (invoicing readiness, SET-R9, plus the
/// address an invoice prints, D342), each location's FSSAI licence through <see cref="ILocationsApi"/> and the sender
/// providers through <see cref="INotificationProviders"/>. Three reads, no write.
/// </summary>
public sealed class StoreReadinessReportService(ISettingsRepository repository, ILocationsApi locations, INotificationProviders providers) : IStoreReadinessReportService
{
    public const string Ok = "OK";
    public const string Missing = "MISSING";
    public const string AddressField = "address";

    public async Task<StoreReadinessResponse> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await repository.GetBusinessProfileAsync(cancellationToken);
        var allLocations = await locations.GetAllFulfillmentLocationsAsync(cancellationToken);
        var channels = providers.GetStatus();

        var missing = InvoicingReadiness.MissingFields(profile);
        var noProfile = profile is null;
        IReadOnlyList<ProfileFieldReadiness> profileChecks =
        [
            Field(InvoicingReadiness.LegalNameField, noProfile || missing.Contains(InvoicingReadiness.LegalNameField)),
            Field(AddressField, noProfile || string.IsNullOrWhiteSpace(profile!.Address)),
            Field(InvoicingReadiness.GstinField, noProfile || missing.Contains(InvoicingReadiness.GstinField)),
            Field(InvoicingReadiness.TimeZoneField, noProfile || missing.Contains(InvoicingReadiness.TimeZoneField)),
        ];

        IReadOnlyList<LocationReadiness> locationChecks = [.. allLocations.Select(l => new LocationReadiness(
            l.Id, l.CityId, l.Code, l.Name, l.Status.ToCode(), string.IsNullOrWhiteSpace(l.FssaiLicenseNo) ? Missing : Ok))];

        IReadOnlyList<ChannelReadiness> channelChecks =
        [
            Channel("SMS", channels.Sms),
            Channel("EMAIL", channels.Email),
            Channel("PUSH", channels.Push),
        ];

        var isReady = locationChecks.Count > 0
            && profileChecks.All(c => c.Status == Ok)
            && locationChecks.All(c => c.FssaiLicenseStatus == Ok)
            && channelChecks.All(c => c.Status == Ok);
        return new StoreReadinessResponse(isReady, profileChecks, locationChecks, channelChecks);
    }

    private static ProfileFieldReadiness Field(string field, bool isMissing) => new(field, isMissing ? Missing : Ok);

    private static ChannelReadiness Channel(string channel, ChannelProviderStatus status) => new(channel, status.Provider, status.Delivers ? Ok : Missing);
}
