using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// Attributes common to the members a schema object owns.
/// </summary>
public abstract class ObjectMember : DatabaseElement
{
    /// <summary>
    /// The kind of member this is.
    /// </summary>
    [JsonIgnore]
    public abstract MemberKind Kind { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The member is not owned by an object in a schema, so it has no address.</exception>
    public override MemberAddress Address => Parent is { Schema: { } schema }
        ? new MemberAddress(schema.Name, Parent.Name, Name, Kind)
        : throw new InvalidOperationException(
            $"'{Name}' has no address because it does not belong to an object in a schema. Add it to one first.");

    /// <summary>
    /// The object that owns the member, or <see langword="null"/> when it has not been placed in a tree.
    /// </summary>
    [JsonIgnore]
    public SchemaObject? Parent
    {
        get;
        internal set
        {
            if (Parent is { } parent && value is not null && !ReferenceEquals(parent, value))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} '{Name}' already belongs to '{parent.Name}' and cannot be attached to " +
                    $"'{value.Name}'; remove it first, or attach a copy instead.");
            }
            field = value;
        }
    }
}
