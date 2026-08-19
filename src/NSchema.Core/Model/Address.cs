using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// The fully-qualified address of something in a database, that points at a node from outside the tree.
/// </summary>
public abstract record Address
{
    /// <summary>
    /// The address as written: its path, dot-qualified.
    /// </summary>
    [JsonIgnore]
    public virtual string Value => string.Join('.', Path);

    /// <summary>
    /// The names leading from the database down to what this addresses.
    /// </summary>
    [JsonIgnore]
    protected abstract IReadOnlyList<SqlIdentifier> Path { get; }

    /// <summary>
    /// Whether anything can live inside what this addresses.
    /// </summary>
    [JsonIgnore]
    protected abstract bool CanContain { get; }

    /// <summary>
    /// Whether this address covers <paramref name="other"/> — it is <paramref name="other"/> or a container
    /// above it. Coverage runs downward through containment only: a schema covers what it holds, never the
    /// reverse.
    /// </summary>
    /// <param name="other">The address to test for coverage.</param>
    public bool Covers(Address other)
    {
        if (other.Path.Count < Path.Count || (other.Path.Count > Path.Count && !CanContain))
        {
            return false;
        }

        for (var i = 0; i < Path.Count; i++)
        {
            if (Path[i] != other.Path[i])
            {
                return false;
            }
        }

        // A deeper address is inside this one, which containment has already decided. At the same depth the
        // two name the same location, so it comes down to whether they name the same thing there.
        return other.Path.Count > Path.Count || NamesSameKindAs(other);
    }

    /// <summary>
    /// Whether an address at this same location names the same kind of thing.
    /// </summary>
    /// <param name="other">The address at the same location.</param>
    protected abstract bool NamesSameKindAs(Address other);

    /// <inheritdoc />
    public sealed override string ToString() => Value;
}
