using System.Text.Json;

namespace FreshOrbit.SharedKernel.Serialization;

/// <summary>The JSON settings shared by the API, the outbox and stored jsonb documents.</summary>
public static class FreshOrbitJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Converters.Any(c => c is EnumCodeJsonConverterFactory))
        {
            options.Converters.Add(new EnumCodeJsonConverterFactory());
        }

        return options;
    }
}
