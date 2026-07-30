using System.Text.Json.Serialization;
using NSchema.Model.Schemas;

namespace NSchema.Model;

/// <summary>
/// Attributes common to the objects a schema owns.
/// </summary>
public abstract class SchemaObject : DatabaseElement
{
    /// <summary>
    /// The schema the object belongs to, or <see langword="null"/> when it has not been placed in a tree.
    /// </summary>
    [JsonIgnore]
    public Schema? Schema
    {
        get;
        internal set
        {
            if (Schema is { } schema && value is not null && !ReferenceEquals(schema, value))
            {
                throw new InvalidOperationException(
                    $"{Kind} '{Name}' already belongs to schema '{schema.Name}' and cannot be attached " +
                    $"to '{value.Name}'; remove it first, or attach a copy instead.");
            }
            field = value;
        }
    }

    /// <summary>
    /// The kind of object this is.
    /// </summary>
    [JsonIgnore]
    public abstract SchemaObjectKind Kind { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The object does not belong to a schema, so it has no address.</exception>
    public override ObjectAddress Address => Schema is { } schema
        ? new ObjectAddress(schema.Name, Name, Kind)
        : throw new InvalidOperationException(
            $"{Kind} '{Name}' has no address because it does not belong to a schema. Add it to one first.");
}
