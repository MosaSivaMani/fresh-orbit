using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FreshOrbit.SharedKernel.EntityFramework.Conventions;

/// <summary>Normalizes <see cref="DateTimeOffset"/> values to UTC before they reach a <c>timestamptz</c> column.</summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
