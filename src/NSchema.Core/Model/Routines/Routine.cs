using System.Diagnostics;

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

    /// <inheritdoc/>
    public override Routine Clone() => new() { Name = Name, RoutineKind = RoutineKind, Arguments = Arguments, Definition = Definition, Comment = Comment };

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
