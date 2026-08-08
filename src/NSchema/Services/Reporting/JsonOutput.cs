using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Model.Serialization;

namespace NSchema.Services.Reporting;

/// <summary>
/// The shared NDJSON serialization the JSON reporter writes results and log events through, so every line follows
/// the same conventions.
/// </summary>
internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // Value objects render as their bare value, addresses structurally; Core owns those conventions, this owns
        // the NDJSON shape (single line, terse) that persistence would not want.
    }.AddModelConverters();

    public static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, Options));
}
