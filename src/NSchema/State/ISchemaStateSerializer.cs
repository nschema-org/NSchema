using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseSchema"/> snapshots for storage in a state backend.
/// </summary>
/// <remarks>
/// Implement this interface to replace the default versioned JSON format, or inject it into a custom
/// <see cref="ISchemaStateStore"/> so the store is decoupled from the serialization format.
/// </remarks>
public interface ISchemaStateSerializer
{
    /// <summary>
    /// Serializes a schema snapshot to a string for storage.
    /// </summary>
    string Serialize(DatabaseSchema schema);

    /// <summary>
    /// Deserializes a schema snapshot from a stored string.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The payload is missing or malformed.</exception>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
    DatabaseSchema Deserialize(string value);
}
