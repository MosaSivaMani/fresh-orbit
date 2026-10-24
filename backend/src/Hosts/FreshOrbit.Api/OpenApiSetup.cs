using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
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
    private static readonly Lazy<HashSet<string>> SharedNames = new(FindNamesSharedByModules);

    public static void Configure(OpenApiOptions options)
    {
        options.CreateSchemaReferenceId = SchemaReferenceId;
        options.AddSchemaTransformer((schema, context, _) =>
        {
            DescribeEnumCodes(schema, context.JsonTypeInfo.Type);
            DropNumberAsString(schema);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Schema ids are type names, so two modules' types with one name (Ordering and Inventory <c>QuantityDto</c>)
    /// collapse into one schema. A name used by more than one module gets the module as prefix
    /// (<c>OrderingQuantityDto</c>, <c>InventoryQuantityDto</c>).
    /// </summary>
    private static string? SchemaReferenceId(JsonTypeInfo typeInfo)
    {
        var id = OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
        var type = Nullable.GetUnderlyingType(typeInfo.Type) ?? typeInfo.Type;
        return id is not null && SharedNames.Value.Contains(type.Name) ? ModuleOf(type) + id : id;
    }

    private static HashSet<string> FindNamesSharedByModules()
        => [.. AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && assembly.GetName().Name?.StartsWith("FreshOrbit.", StringComparison.Ordinal) == true)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .Where(group => group.Select(ModuleOf).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => group.Key)];

    /// <summary><c>FreshOrbit.Ordering.Application.Orders.QuantityDto</c> → <c>Ordering</c>.</summary>
    private static string ModuleOf(Type type)
    {
        var parts = (type.Namespace ?? string.Empty).Split('.');
        return parts.Length > 1 ? parts[1] : string.Empty;
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
