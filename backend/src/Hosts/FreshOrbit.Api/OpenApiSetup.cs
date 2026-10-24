using System.Text.Json.Nodes;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FreshOrbit.Api;

/// <summary>
/// Document metadata fixes so the published OpenAPI matches what the API writes (clients generate types from it).
/// Runtime behaviour is unchanged.
/// </summary>
internal static class OpenApiSetup
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddSchemaTransformer((schema, context, _) =>
        {
            DescribeEnumCodes(schema, context.JsonTypeInfo.Type);
            DropNumberAsString(schema);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Enums are written as UPPER_SNAKE codes by <c>EnumCodeJsonConverterFactory</c>, which the generator cannot
    /// see; describe them as string enums of their codes.
    /// </summary>
    private static void DescribeEnumCodes(OpenApiSchema schema, Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        var enumType = nullable ?? type;
        if (!enumType.IsEnum)
        {
            return;
        }

        schema.Type = nullable is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
        schema.Format = null;
        schema.Pattern = null;
        schema.Enum = [.. EnumCodes.AllCodes(enumType).Select(code => (JsonNode)JsonValue.Create(code))];
    }

    /// <summary>
    /// Web JSON defaults read numbers from strings too, so the generator types every number as "integer or string".
    /// The API always writes numbers; publish them as plain numbers.
    /// </summary>
    private static void DropNumberAsString(OpenApiSchema schema)
    {
        if (schema.Type is not { } type
            || (type & JsonSchemaType.String) == 0
            || (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) == 0)
        {
            return;
        }

        schema.Type = type & ~JsonSchemaType.String;
        schema.Pattern = null;
    }
}
