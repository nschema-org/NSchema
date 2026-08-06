using System.Diagnostics;
using NSchema.Model.Routines;

namespace NSchema.Model.Triggers;

/// <summary>
/// Represents a trigger on a table fired on a table operation.
/// </summary>
[DebuggerDisplay("{Name,nq} (trigger)")]
public sealed class Trigger : ObjectMember, IEquatable<Trigger>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.Trigger;

    /// <summary>
    /// When the trigger fires relative to the operation.
    /// </summary>
    public required TriggerTiming Timing { get; set; }

    /// <summary>
    /// The operation(s) that fire the trigger.
    /// </summary>
    public required TriggerEvent Events { get; set; }

    /// <summary>
    /// The function the trigger executes (optionally schema-qualified); null for an inline-body trigger.
    /// </summary>
    public RoutineReference? Function { get; set; }

    /// <summary>
    /// Whether the trigger fires per row or per statement.
    /// </summary>
    public TriggerLevel Level { get; set; } = TriggerLevel.Statement;

    /// <summary>
    /// The columns an <c>UPDATE</c> trigger is narrowed to (<c>UPDATE OF (…)</c>), if any.
    /// </summary>
    public List<SqlIdentifier> UpdateOfColumns { get; init; } = [];

    /// <summary>
    /// An optional <c>WHEN</c> condition, stored verbatim (opaque SQL).
    /// </summary>
    public SqlText? When { get; set; }

    /// <summary>
    /// Optional literal arguments passed to the function, stored verbatim; <see langword="null"/> when none.
    /// </summary>
    public SqlText? FunctionArguments { get; set; }

    /// <summary>
    /// The trigger's inline statement body, stored verbatim (opaque SQL); <see langword="null"/> for a
    /// function-style trigger.
    /// </summary>
    public SqlText? Body { get; set; }

    /// <summary>
    /// The objects the trigger references.
    /// An unqualified reference resolves against <paramref name="schema"/> (the owning table's).
    /// </summary>
    public IReadOnlyList<ObjectAddress> References(SqlIdentifier schema)
    {
        var result = new List<ObjectAddress>();
        if (Function is { } function)
        {
            result.Add(new ObjectAddress(function.Schema ?? schema, function.Name));
        }
        if (Body is { } body)
        {
            result.AddRange(Services.RoutineDependencyExtractor.Extract(body, schema));
        }
        if (When is { } when)
        {
            result.AddRange(Services.ExpressionDependencyScanner.CallSites(when.Value, schema));
        }
        return [.. result.Distinct()];
    }

    /// <inheritdoc/>
    public override Trigger Clone() => new()
    {
        Name = Name,
        Timing = Timing,
        Events = Events,
        Function = Function,
        Level = Level,
        UpdateOfColumns = [.. UpdateOfColumns],
        When = When,
        FunctionArguments = FunctionArguments,
        Body = Body,
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition;.
    /// </summary>
    public bool Equals(Trigger? other) =>
        other is not null
        && Name == other.Name
        && Timing == other.Timing
        && Events == other.Events
        && Equals(Function, other.Function)
        && Level == other.Level
        && UpdateOfColumns.SequenceEqual(other.UpdateOfColumns)
        && Equals(When, other.When)
        && Equals(FunctionArguments, other.FunctionArguments)
        && Equals(Body, other.Body);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Trigger other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Timing, Events, Function, Level, When, FunctionArguments, Body);
}
