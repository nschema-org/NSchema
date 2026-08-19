using System.Diagnostics;
using System.Text.Json.Serialization;

namespace NSchema.Model.Routines;

/// <summary>
/// Represents a database routine. A function or a procedure (see <see cref="RoutineKind"/>).
/// </summary>
[DebuggerDisplay("{Name,nq} ({RoutineKind})")]
public sealed class Routine : SchemaObject, IEquatable<Routine>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.Routine;

    /// <summary>
    /// Whether the routine is a function or a procedure.
    /// </summary>
    public required RoutineKind RoutineKind { get; set; }

    /// <summary>
    /// The argument list, stored verbatim (the text inside the parentheses; may be empty).
    /// </summary>
    public required SqlText Arguments { get; set; }

    /// <summary>
    /// Everything after the argument list, stored verbatim.
    /// </summary>
    public required SqlText Definition { get; set; }

    /// <summary>
    /// The objects the definition references, scanned at projection.
    /// </summary>
    [JsonIgnore]
    [Obsolete("Dependencies now live on the dependency graph.")]
    public List<ObjectAddress> DependsOn { get; init; } = [];

    /// <summary>
    /// The objects the definition references.
    /// An unqualified reference resolves against <paramref name="schema"/> (the routine's own).
    /// </summary>
    public IReadOnlyList<ObjectAddress> References(SqlIdentifier schema) =>
        Services.RoutineDependencyExtractor.Extract(Definition, schema, RoutineKind);

    /// <inheritdoc/>
    public override Routine Clone() => new()
    {
        Name = Name,
        RoutineKind = RoutineKind,
        Arguments = Arguments,
        Definition = Definition,
#pragma warning disable CS0618 // Type or member is obsolete
        DependsOn = [.. DependsOn],
#pragma warning restore CS0618 // Type or member is obsolete
        ProvidedBy = ProvidedBy,
        Comment = Comment
    };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(Routine? other) =>
        other is not null
        && Name == other.Name
        && RoutineKind == other.RoutineKind
        && Arguments == other.Arguments
        && Definition == other.Definition;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Routine other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, RoutineKind, Arguments, Definition);
}
