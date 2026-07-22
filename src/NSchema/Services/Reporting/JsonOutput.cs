using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Model.Services;

namespace NSchema.Services.Reporting;

/// <summary>
/// The shared NDJSON serialization used by both the JSON messenger and presenter, so the two write identical output
/// without sharing a base class.
/// </summary>
internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Value objects render as their bare value; SemanticVersion and VersionRange carry their own [JsonConverter].
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new ValueObjectJsonConverter() },
    };

    public static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, Options));
}
