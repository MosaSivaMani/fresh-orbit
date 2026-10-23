using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FreshOrbit.SharedKernel.EntityFramework.Conventions;

/// <summary>Stores an enum as its UPPER_SNAKE code (see <see cref="EnumCodes"/>).</summary>
public sealed class EnumCodeConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public EnumCodeConverter()
        : base(value => value.ToCode(), code => code.ToEnum<TEnum>())
    {
    }
}
