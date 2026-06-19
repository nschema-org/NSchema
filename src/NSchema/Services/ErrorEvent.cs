using System.Text.Json.Serialization;

namespace NSchema.Services;

/// <summary>
/// The <c>{"type":"error","message":…}</c> NDJSON event, emitted when an operation fails.
/// </summary>
internal sealed record ErrorEvent(string Message)
{
    [JsonPropertyOrder(-1)]
    public string Type => "error";
}
