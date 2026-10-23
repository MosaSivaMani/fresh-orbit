using System.Globalization;
using FreshOrbit.Settings.Application.Shared;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Settings.Application.Numbering;

public sealed record SaveNumberSeriesRequest(string Prefix, int PadWidth, long NextValue);

public sealed record NumberSeriesResponse(
    string Code,
    string Period,
    string Prefix,
    int PadWidth,
    long NextValue,
    string NextNumberPreview,
    bool IsInUse,
    DateTimeOffset? LastIssuedAt,
    string Version);

public sealed record NumberSeriesListResponse(IReadOnlyList<NumberSeriesResponse> Items);

public interface INumberSeriesService
{
    Task<NumberSeriesListResponse> ListAsync(CancellationToken cancellationToken);

    Task<NumberSeriesResponse> SaveAsync(string code, string period, SaveNumberSeriesRequest request, WriteCondition condition, CancellationToken cancellationToken);

    /// <summary>Draws the next number (row locked in the caller's transaction; saved by the caller).</summary>
    Task<string> NextNumberAsync(string code, string period, CancellationToken cancellationToken);

    /// <summary>Financial-year label of <paramref name="at"/> in the business time zone.</summary>
    Task<string> PeriodForAsync(DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed class NumberSeriesService(ISettingsRepository repository, IUnitOfWork unitOfWork, IClock clock) : INumberSeriesService
{
    public async Task<NumberSeriesListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await repository.ListNumberSeriesAsync(cancellationToken);
        return new NumberSeriesListResponse([.. rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Period, StringComparer.Ordinal).Select(ToResponse)]);
    }

    public async Task<NumberSeriesResponse> SaveAsync(string code, string period, SaveNumberSeriesRequest request, WriteCondition condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(condition);
        NumberSeries.EnsureKey(code, period);
        var existing = await repository.GetNumberSeriesAsync(code, period, cancellationToken);
        NumberSeries series;
        if (condition.ShouldCreate(existing, unitOfWork, "number series"))
        {
            series = NumberSeries.Create(code, period, request.Prefix ?? string.Empty, request.PadWidth, request.NextValue);
            repository.Add(series);
        }
        else
        {
            series = existing!;
            series.Change(request.Prefix ?? string.Empty, request.PadWidth, request.NextValue);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(series);
    }

    public async Task<string> NextNumberAsync(string code, string period, CancellationToken cancellationToken)
    {
        var series = await repository.LockNumberSeriesAsync(code, period, cancellationToken)
            ?? throw new DomainException(SettingsErrors.ConfigurationMissing, $"No {code} number series exists for period {period}; an admin must add it in settings.");
        return series.Next(clock.UtcNow);
    }

    public async Task<string> PeriodForAsync(DateTimeOffset at, CancellationToken cancellationToken)
    {
        var profile = await repository.GetBusinessProfileAsync(cancellationToken)
            ?? throw new DomainException(SettingsErrors.ConfigurationMissing, "The business profile (with its time zone) must be saved before numbers can be issued.");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(profile.TimeZone, out var zone))
        {
            throw new DomainException(SettingsErrors.ConfigurationMissing, "The business profile time zone is not a known time zone.");
        }

        return FinancialYear.LabelFor(at, zone);
    }

    public static NumberSeriesResponse ToResponse(NumberSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return new NumberSeriesResponse(
            series.Code,
            series.Period,
            series.Prefix,
            series.PadWidth,
            series.NextValue,
            series.Format(series.NextValue),
            series.IsInUse,
            series.LastIssuedAt,
            series.Version.ToString(CultureInfo.InvariantCulture));
    }
}
