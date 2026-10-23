using System.Text.Json;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Catalog.Infrastructure.Seeding;

/// <summary>
/// SYNC load of <c>catalog/preparation-methods.json</c> (reference-data.md §1, §4): upsert by code, never delete.
/// The whole file is validated before any write; one save per file.
/// </summary>
public sealed partial class PreparationMethodsLoader(FreshOrbitDbContext db, ILogger<PreparationMethodsLoader> logger) : IReferenceDataLoader
{
    public const string RelativePath = "catalog/preparation-methods.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 200;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = Path.Combine(context.DirectoryPath, RelativePath);
        ReferenceFile? file;
        await using (var stream = File.OpenRead(path))
        {
            file = await JsonSerializer.DeserializeAsync<ReferenceFile>(stream, JsonOptions, cancellationToken);
        }

        var items = Validate(file, path);
        var existing = await db.Set<PreparationMethod>().ToDictionaryAsync(m => m.Code, StringComparer.Ordinal, cancellationToken);
        int inserted = 0, updated = 0, skipped = 0;
        foreach (var item in items)
        {
            if (existing.TryGetValue(item.Code!, out var method))
            {
                if (method.Sync(item.Name!.Trim(), item.Description?.Trim(), item.SortOrder, item.GuidanceYieldBp))
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }
            else
            {
                db.Add(new PreparationMethod(item.Code!, item.Name!.Trim(), item.Description?.Trim(), item.SortOrder, item.GuidanceYieldBp));
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(RelativePath, inserted, updated, skipped);
    }

    private static List<ReferenceItem> Validate(ReferenceFile? file, string path)
    {
        if (file?.Items is not { Count: > 0 } items || file.Version < 1)
        {
            throw new InvalidDataException($"{path}: expected {{ \"version\": n, \"items\": [...] }} with at least one item.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Code is not { Length: <= 40 } code || !ErrorCodes.IsUpperSnake(code) || !codes.Add(code))
            {
                throw new InvalidDataException($"{path}: items[{i}].code must be a unique UPPER_SNAKE code of at most 40 characters.");
            }

            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 80 || item.Description is { Length: > 500 })
            {
                throw new InvalidDataException($"{path}: items[{i}] needs a name of 1 to 80 characters and a description of at most 500.");
            }

            if (item.GuidanceYieldBp is < 1 or > BasisPoints.Whole)
            {
                throw new InvalidDataException($"{path}: items[{i}].guidanceYieldBp must be between 1 and 10000.");
            }
        }

        return [.. items];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data {File}: {Inserted} inserted, {Updated} updated, {Skipped} unchanged")]
    private partial void LogLoaded(string file, int inserted, int updated, int skipped);

    private sealed record ReferenceFile(int Version, IReadOnlyList<ReferenceItem>? Items);

    private sealed record ReferenceItem(string? Code, string? Name, string? Description, int SortOrder, int GuidanceYieldBp);
}
