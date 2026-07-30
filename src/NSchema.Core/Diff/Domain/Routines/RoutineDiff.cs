using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Diff.Domain.Routines;

/// <summary>
/// Describes a change to a routine.
/// </summary>
public sealed record RoutineDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private RoutineDiff() { }

    /// <summary>
    /// The name of the schema the routine belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, SchemaObjectKind.Routine);

    /// <summary>
    /// The routine name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the routine.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// Whether the routine is a function or a procedure (carried so the correct statement is emitted for a rename, comment change, or removal).
    /// </summary>
    public required RoutineKind RoutineKind { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added routine; otherwise <see langword="null"/>.
    /// </summary>
    public Routine? Definition { get; init; }

    /// <summary>
    /// The change to the argument list, set when the signature changed (which forces a recreate).
    /// </summary>
    public ValueChange<SqlText>? Arguments { get; init; }

    /// <summary>
    /// The change to the routine's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a routine being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A routine being created, named by its own definition.
    /// </summary>
    public static RoutineDiff Added(SqlIdentifier schema, Routine definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        RoutineKind = definition.RoutineKind,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A routine being dropped.
    /// </summary>
    public static RoutineDiff Removed(SqlIdentifier schema, SqlIdentifier name, RoutineKind routineKind) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove, RoutineKind = routineKind };

    /// <summary>
    /// A routine altered in place; the individual changes are set on the result.
    /// </summary>
    public static RoutineDiff Modified(SqlIdentifier schema, SqlIdentifier name, RoutineKind routineKind) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Modify, RoutineKind = routineKind };

    /// <summary>
    /// The signature changed, so the routine must be dropped and recreated: replacing in place would leave the
    /// old signature behind as a separate overload in the database.
    /// </summary>
    public bool RequiresRecreate => Arguments is not null;
}
