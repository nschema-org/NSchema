using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NSchema.Model.Serialization;

internal static class ModelSerialization
{
    public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { InheritJsonIgnore }
        }
    }.AddModelConverters();

    // https://github.com/dotnet/runtime/issues/50078#issuecomment-2192460403
    private static void InheritJsonIgnore(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = 0; i < jsonTypeInfo.Properties.Count; i++)
        {
            if (jsonTypeInfo.Properties[i].AttributeProvider is not PropertyInfo propertyInfo)
            {
                continue;
            }

            if (!IsJsonIgnored(propertyInfo))
            {
                continue;
            }

            jsonTypeInfo.Properties.RemoveAt(i--);
        }
    }

    // GetCustomAttribute does not follow overridden properties so walk the declaring-type chain for a [JsonIgnore].
    // Only an unconditional ignore removes the property: a conditional one (e.g. WhenWritingDefault) keeps its own semantics.
    private static bool IsJsonIgnored(PropertyInfo property)
    {
        for (var type = property.DeclaringType; type is not null; type = type.BaseType)
        {
            var prop = type.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop?.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false) is { Condition: JsonIgnoreCondition.Always })
            {
                return true;
            }
        }

        return false;
    }

}
