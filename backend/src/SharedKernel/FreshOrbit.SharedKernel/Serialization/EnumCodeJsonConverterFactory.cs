using System.Text.Json;
using System.Text.Json.Serialization;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.SharedKernel.Serialization;

/// <summary>System.Text.Json converter writing enums as their UPPER_SNAKE codes (see <see cref="EnumCodes"/>).</summary>
public sealed class EnumCodeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(EnumCodeJsonConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class EnumCodeJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var code = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            return EnumCodes.TryToEnum<TEnum>(code, out var value)
                ? value
                : throw new JsonException($"'{code}' is not a {typeof(TEnum).Name} code.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.ToCode());
        }

        public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Read(ref reader, typeToConvert, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WritePropertyName(value.ToCode());
        }
    }
}
