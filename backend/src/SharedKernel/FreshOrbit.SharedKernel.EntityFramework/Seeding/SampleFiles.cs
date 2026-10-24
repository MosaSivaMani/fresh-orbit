using System.Text.Json;
using FreshOrbit.SharedKernel.Serialization;

namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>Reading sample files and turning their offsets into instants (docs/product/sample-data.md §3).</summary>
public static class SampleFiles
{
    /// <summary>Deserializes <c>{DirectoryPath}/{fileName}</c> (camelCase, enums as UPPER_SNAKE codes).</summary>
    public static async Task<T> ReadAsync<T>(SampleDataContext context, string fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = Path.Combine(context.DirectoryPath, fileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, FreshOrbitJson.Options, cancellationToken)
            ?? throw new InvalidDataException($"{path} is empty.");
    }

    /// <summary>The city-local date of T0 plus <paramref name="offsetDays"/>.</summary>
    public static DateOnly LocalDate(DateTimeOffset startedAt, string timeZoneId, int offsetDays)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startedAt, zone).DateTime).AddDays(offsetDays);
    }

    /// <summary>The UTC instant local midnight starts on the city-local date of T0 plus <paramref name="offsetDays"/>.</summary>
    public static DateTimeOffset LocalMidnight(DateTimeOffset startedAt, string timeZoneId, int offsetDays)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = LocalDate(startedAt, timeZoneId, offsetDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>A missing natural key fails the run with the key named (rule 7: never skip silently).</summary>
    public static InvalidDataException Missing(string fileName, string what, string key)
        => new($"{fileName}: no {what} has the key '{key}'; load the files in order (sample-set.json).");
}
