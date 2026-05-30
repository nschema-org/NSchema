using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseSchema"/> snapshots to the versioned state envelope.
/// </summary>
internal static class SchemaStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new SqlTypeJsonConverter(), new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serializes a schema snapshot to its JSON envelope representation.
    /// </summary>
    public static string Serialize(DatabaseSchema schema)
    {
        var envelope = new SchemaStateEnvelope(SchemaStateEnvelope.CurrentVersion, schema);
        return JsonSerializer.Serialize(envelope, _options);
    }

    /// <summary>
    /// Deserializes a schema snapshot from its JSON envelope representation.
    /// </summary>
    /// <exception cref="JsonException">The payload is missing or malformed.</exception>
    /// <exception cref="NotSupportedException">The envelope was written by an incompatible newer format version.</exception>
    public static DatabaseSchema Deserialize(string json)
    {
        var envelope = JsonSerializer.Deserialize<SchemaStateEnvelope>(json, _options)
            ?? throw new JsonException("State payload deserialized to null.");

        if (envelope.Version > SchemaStateEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"State format version {envelope.Version} is newer than the supported version " +
                $"{SchemaStateEnvelope.CurrentVersion}. Upgrade NSchema to read this state.");
        }

        return envelope.Schema;
    }
}
