using System.Text.Json;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.OrderPolicies;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Settings.Infrastructure.Seeding;

/// <summary>
/// Loads <c>settings/order-policies.json</c> and <c>settings/number-series.json</c> (INSERT_IF_MISSING, SET-R7):
/// existing rows are never touched, so admin edits and issued series stay as they are.
/// </summary>
public sealed partial class SettingsReferenceDataLoader(FreshOrbitDbContext db, ILogger<SettingsReferenceDataLoader> logger) : IReferenceDataLoader
{
    public int Order => 30;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var directory = Path.Combine(context.DirectoryPath, "settings");
        await LoadOrderPoliciesAsync(Path.Combine(directory, "order-policies.json"), cancellationToken);
        await LoadNumberSeriesAsync(Path.Combine(directory, "number-series.json"), cancellationToken);
    }

    private async Task LoadOrderPoliciesAsync(string path, CancellationToken cancellationToken)
    {
        var file = await ReadAsync<OrderPoliciesFile>(path, cancellationToken);
        if (file.Policies is null || file.Policies.Count == 0 || file.Policies.Any(p => p.CityId is not null))
        {
            throw new InvalidDataException($"{path}: 'policies' must hold the default policy only (cityId null).");
        }

        var datas = file.Policies.Select(p => new OrderPolicyData(p.ReservationMinutes, p.MaxOverweightBp, p.CancelAllowedUntilStatus ?? string.Empty, p.CodMaxOrderPaise, p.EnabledPaymentMethods ?? [], p.SubstitutionResponseMinutes, p.ComplaintWindowHours, p.MaxDeliveryAttempts)).ToList();
        var invalid = datas.SelectMany(OrderPolicy.Validate).ToList();
        if (invalid.Count > 0)
        {
            throw new InvalidDataException($"{path}: {string.Join("; ", invalid.Select(e => e.Message))}");
        }

        var inserted = 0;
        if (!await db.Set<OrderPolicy>().AnyAsync(p => p.CityId == null, cancellationToken))
        {
            db.Add(OrderPolicy.Create(null, datas[0]));
            inserted = 1;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded("order-policies.json", inserted, file.Policies.Count - inserted);
    }

    private async Task LoadNumberSeriesAsync(string path, CancellationToken cancellationToken)
    {
        var file = await ReadAsync<NumberSeriesFile>(path, cancellationToken);
        if (file.Series is null || file.Series.Count == 0)
        {
            throw new InvalidDataException($"{path}: 'series' must not be empty.");
        }

        // Validate every row before any write.
        var rows = file.Series.Select(s => NumberSeries.Create(s.Code ?? string.Empty, s.Period ?? string.Empty, s.Prefix ?? string.Empty, s.PadWidth, s.NextValue)).ToList();
        var existing = (await db.Set<NumberSeries>().Select(s => new { s.Code, s.Period }).ToListAsync(cancellationToken))
            .Select(s => (s.Code, s.Period)).ToHashSet();
        var inserted = 0;
        foreach (var row in rows.Where(r => !existing.Contains((r.Code, r.Period))))
        {
            db.Add(row);
            inserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded("number-series.json", inserted, rows.Count - inserted);
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, FreshOrbitJson.Options, cancellationToken)
            ?? throw new InvalidDataException($"{path} is empty.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data {File}: {Inserted} inserted, {Skipped} skipped")]
    private partial void LogLoaded(string file, int inserted, int skipped);

    private sealed record OrderPoliciesFile(int Version, List<OrderPolicyRow>? Policies);

    private sealed record OrderPolicyRow(
        Guid? CityId,
        int ReservationMinutes,
        int MaxOverweightBp,
        string? CancelAllowedUntilStatus,
        long CodMaxOrderPaise,
        List<string>? EnabledPaymentMethods,
        int SubstitutionResponseMinutes,
        int ComplaintWindowHours,
        int MaxDeliveryAttempts);

    private sealed record NumberSeriesFile(int Version, List<NumberSeriesRow>? Series);

    private sealed record NumberSeriesRow(string? Code, string? Period, string? Prefix, int PadWidth, long NextValue);
}
