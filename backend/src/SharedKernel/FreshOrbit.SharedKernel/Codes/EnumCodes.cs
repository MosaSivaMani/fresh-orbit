using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.SharedKernel.Codes;

/// <summary>
/// Converts enum members to and from their stored UPPER_SNAKE codes.
/// <para>
/// The code of a member is its <see cref="CodeAttribute"/> value or else its name converted to UPPER_SNAKE:
/// an underscore goes before an upper-case letter that follows a lower-case letter or digit, and before the last
/// capital of an acronym followed by a lower-case letter (<c>CurryCut → CURRY_CUT</c>, <c>PerKg → PER_KG</c>,
/// <c>UpiOnDelivery → UPI_ON_DELIVERY</c>, <c>HTTPServer → HTTP_SERVER</c>). Digits stay attached
/// (<c>Level2 → LEVEL2</c>). Parsing is exact and case-sensitive.
/// </para>
/// </summary>
public static class EnumCodes
{
    private static readonly ConcurrentDictionary<Type, EnumCodeMap> Maps = new();

    public static string ToCode<TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => GetMap(typeof(TEnum)).ToCode(value);

    /// <summary>Non-generic variant for boxed enum values.</summary>
    public static string ToCode(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return GetMap(value.GetType()).ToCode(value);
    }

    /// <summary>Parses a stored code; throws <see cref="ArgumentException"/> for an unknown code.</summary>
    public static TEnum ToEnum<TEnum>(this string code)
        where TEnum : struct, Enum
        => TryToEnum<TEnum>(code, out var value)
            ? value
            : throw new ArgumentException($"'{code}' is not a {typeof(TEnum).Name} code.", nameof(code));

    public static bool TryToEnum<TEnum>(string? code, out TEnum value)
        where TEnum : struct, Enum
    {
        if (code is not null && GetMap(typeof(TEnum)).TryParse(code, out var boxed))
        {
            value = (TEnum)boxed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Non-generic parse used by converters.</summary>
    public static object ToEnum(Type enumType, string code)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        return GetMap(enumType).TryParse(code, out var value)
            ? value
            : throw new ArgumentException($"'{code}' is not a {enumType.Name} code.", nameof(code));
    }

    public static bool IsCode<TEnum>(string? code)
        where TEnum : struct, Enum
        => code is not null && GetMap(typeof(TEnum)).TryParse(code, out _);

    /// <summary>All codes of <typeparamref name="TEnum"/> in declaration order (for CHECK constraints).</summary>
    public static IReadOnlyList<string> AllCodes<TEnum>()
        where TEnum : struct, Enum
        => GetMap(typeof(TEnum)).Codes;

    public static IReadOnlyList<string> AllCodes(Type enumType) => GetMap(enumType).Codes;

    /// <summary>Converts a PascalCase identifier to UPPER_SNAKE.</summary>
    public static string ToUpperSnake(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                continue;
            }

            if (char.IsUpper(c) && i > 0 && builder.Length > 0 && builder[^1] != '_')
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static EnumCodeMap GetMap(Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"{enumType.Name} is not an enum.", nameof(enumType));
        }

        return Maps.GetOrAdd(enumType, static t => new EnumCodeMap(t));
    }

    private sealed class EnumCodeMap
    {
        private readonly FrozenDictionary<object, string> _toCode;
        private readonly FrozenDictionary<string, object> _toValue;

        public EnumCodeMap(Type enumType)
        {
            var toCode = new Dictionary<object, string>();
            var toValue = new Dictionary<string, object>(StringComparer.Ordinal);
            var codes = new List<string>();
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var value = field.GetValue(null)!;
                var code = field.GetCustomAttribute<CodeAttribute>()?.Code ?? ToUpperSnake(field.Name);
                if (!ErrorCodes.IsUpperSnake(code))
                {
                    throw new InvalidOperationException($"{enumType.Name}.{field.Name} has code '{code}', which is not UPPER_SNAKE.");
                }

                if (!toValue.TryAdd(code, value))
                {
                    throw new InvalidOperationException($"{enumType.Name} has the code '{code}' more than once.");
                }

                toCode.TryAdd(value, code);
                codes.Add(code);
            }

            _toCode = toCode.ToFrozenDictionary();
            _toValue = toValue.ToFrozenDictionary(StringComparer.Ordinal);
            Codes = codes.AsReadOnly();
        }

        public IReadOnlyList<string> Codes { get; }

        public string ToCode(object value) => _toCode.TryGetValue(value, out var code)
            ? code
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not a defined enum member.");

        public bool TryParse(string code, out object value) => _toValue.TryGetValue(code, out value!);
    }
}
