using System.Text.Json;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Pricing.Infrastructure.Seeding;

/// <summary>
/// SYNC load of <c>pricing/tax-categories.json</c> (reference-data.md §6): codes and names only, never rates; upsert by
/// code, never delete. The file is validated before any write; one save.
/// </summary>
public sealed partial class TaxCategoriesLoader(FreshOrbitDbContext db, ILogger<TaxCategoriesLoader> logger) : IReferenceDataLoader
{
    public const string RelativePath = "pricing/tax-categories.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 210;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = Path.Combine(context.DirectoryPath, RelativePath);
        ReferenceFile? file;
        await using (var stream = File.OpenRead(path))
        {
            file = await JsonSerializer.DeserializeAsync<ReferenceFile>(stream, JsonOptions, cancellationToken);
        }

        if (file?.Items is not { Count: > 0 } items || file.Version < 1)
        {
            throw new InvalidDataException($"{path}: expected {{ \"version\": n, \"items\": [...] }} with at least one item.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Code is not { Length: <= 40 } code || !ErrorCodes.IsUpperSnake(code) || !codes.Add(code)
                || string.IsNullOrWhiteSpace(items[i].Name) || items[i].Name!.Length > 80 || items[i].AppliesTo is { Length: > 200 })
            {
                throw new InvalidDataException($"{path}: items[{i}] needs a unique UPPER_SNAKE code (≤ 40), a name (≤ 80) and appliesTo (≤ 200).");
            }
        }

        var existing = await db.Set<TaxCategory>().ToDictionaryAsync(c => c.Code, StringComparer.Ordinal, cancellationToken);
        int inserted = 0, updated = 0, skipped = 0;
        foreach (var item in items)
        {
            if (!existing.TryGetValue(item.Code!, out var category))
            {
                db.Add(new TaxCategory(item.Code!, item.Name!.Trim(), item.AppliesTo?.Trim()));
                inserted++;
            }
            else if (category.Sync(item.Name!.Trim(), item.AppliesTo?.Trim()))
            {
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(RelativePath, inserted, updated, skipped);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data {File}: {Inserted} inserted, {Updated} updated, {Skipped} unchanged")]
    private partial void LogLoaded(string file, int inserted, int updated, int skipped);

    private sealed record ReferenceFile(int Version, IReadOnlyList<ReferenceItem>? Items);

    private sealed record ReferenceItem(string? Code, string? Name, string? AppliesTo);
}
