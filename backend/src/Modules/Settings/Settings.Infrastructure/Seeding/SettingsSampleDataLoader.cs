using FluentValidation;
using FreshOrbit.Settings.Application.BusinessProfiles;
using FreshOrbit.Settings.Application.Shared;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Settings.Infrastructure.Seeding;

/// <summary>
/// Sample <c>settings.json</c> (order 5): the business profile, only when none exists (D59), through
/// <see cref="IBusinessProfileService"/> with <c>If-None-Match: *</c> semantics; a real profile is never replaced.
/// </summary>
public sealed partial class SettingsSampleDataLoader(
    IBusinessProfileService profiles,
    IValidator<SaveBusinessProfileRequest> validator,
    FreshOrbitDbContext db,
    ILogger<SettingsSampleDataLoader> logger) : ISampleDataLoader
{
    public int Order => 5;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<SettingsFile>(context, "settings.json", cancellationToken);
        var row = file.BusinessProfile ?? throw new InvalidDataException("settings.json: 'businessProfile' is missing.");
        if (await db.Set<BusinessProfile>().AnyAsync(cancellationToken))
        {
            LogLoaded(0, 1);
            return;
        }

        var request = new SaveBusinessProfileRequest(row.LegalName, row.TradeName, row.Gstin, row.FssaiLicenseNo, row.Address, row.SupportPhone, row.SupportEmail, row.TimeZone);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await profiles.SaveAsync(request, new WriteCondition(null, CreateOnly: true), cancellationToken);
        var id = await db.Set<BusinessProfile>().Select(p => p.Id).SingleAsync(cancellationToken);
        context.Track<BusinessProfile>(db, id);
        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(1, 0);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample business profile: {Inserted} inserted, {Skipped} skipped because a profile already exists")]
    private partial void LogLoaded(int inserted, int skipped);

    private sealed record SettingsFile(ProfileRow? BusinessProfile);

    private sealed record ProfileRow(string LegalName, string TradeName, string? Gstin, string? FssaiLicenseNo, string Address, string SupportPhone, string SupportEmail, string TimeZone);
}
