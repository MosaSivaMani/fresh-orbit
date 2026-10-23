using System.Text.Json;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Seeding;

/// <summary>
/// Loads <c>inventory/storage-units.json</c> (cold-chain ranges, D11/D23) into <c>inventory.storage_units</c>:
/// SYNC by code (insert missing, update changed, never delete). The file is validated before any write.
/// </summary>
public sealed class StorageUnitsReferenceDataLoader(FreshOrbitDbContext db) : IReferenceDataLoader
{
    public const string RelativePath = "inventory/storage-units.json";

    public int Order => 300;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = Path.Combine(context.DirectoryPath, RelativePath);
        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<StorageUnitsFile>(stream, FreshOrbitJson.Options, cancellationToken)
            ?? throw new InvalidDataException($"{RelativePath} is empty.");
        Validate(file);

        var existing = await db.Set<StorageUnitRange>().ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);
        foreach (var unit in file.StorageUnits)
        {
            if (existing.TryGetValue(unit.Code, out var range))
            {
                if (range.MinC != unit.MinC || range.MaxC != unit.MaxC || range.Description != unit.Description)
                {
                    range.Update(unit.MinC, unit.MaxC, unit.Description);
                }
            }
            else
            {
                db.Add(new StorageUnitRange(unit.Code, unit.MinC, unit.MaxC, unit.Description));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(StorageUnitsFile file)
    {
        if (file.Version < 1 || file.StorageUnits is not { Count: > 0 })
        {
            throw new InvalidDataException($"{RelativePath} needs a version and at least one storage unit.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in file.StorageUnits)
        {
            if (!ErrorCodes.IsUpperSnake(unit.Code) || unit.Code.Length > 40 || !codes.Add(unit.Code))
            {
                throw new InvalidDataException($"{RelativePath}: storage unit code '{unit.Code}' is not a unique UPPER_SNAKE code.");
            }

            if ((unit.MinC is null && unit.MaxC is null) || unit.MinC > unit.MaxC || OutOfColumn(unit.MinC) || OutOfColumn(unit.MaxC))
            {
                throw new InvalidDataException($"{RelativePath}: storage unit '{unit.Code}' needs a valid range between -99.9 and 99.9 °C.");
            }

            if (string.IsNullOrWhiteSpace(unit.Description))
            {
                throw new InvalidDataException($"{RelativePath}: storage unit '{unit.Code}' needs a description.");
            }
        }
    }

    private static bool OutOfColumn(decimal? value) => value is < -99.9m or > 99.9m || (value is { } v && decimal.Round(v, 1) != v);

    private sealed record StorageUnitsFile(int Version, IReadOnlyList<StorageUnitEntry> StorageUnits);

    private sealed record StorageUnitEntry(string Code, decimal? MinC, decimal? MaxC, string Description);
}
