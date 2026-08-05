using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// The recorded spellings of a database's body-bearing objects, structured by kind.
/// </summary>
public sealed record DefinitionSet(
    IReadOnlyList<ViewDefinition>? Views = null,
    IReadOnlyList<RoutineDefinition>? Routines = null,
    IReadOnlyList<TriggerDefinition>? Triggers = null
    )
{
    /// <summary>
    /// The set containing no definitions.
    /// </summary>
    public static DefinitionSet Empty { get; } = new();

    /// <summary>
    /// The view definitions in the set.
    /// </summary>
    public IReadOnlyList<ViewDefinition> Views { get; init; } = Views ?? [];

    /// <summary>
    /// The routine definitions in the set.
    /// </summary>
    public IReadOnlyList<RoutineDefinition> Routines { get; init; } = Routines ?? [];

    /// <summary>
    /// The trigger definitions in the set.
    /// </summary>
    public IReadOnlyList<TriggerDefinition> Triggers { get; init; } = Triggers ?? [];

    /// <summary>
    /// Whether the set contains no definitions.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Views.Count == 0 && Routines.Count == 0 && Triggers.Count == 0;

    /// <summary>
    /// The definition recorded for the view at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public ViewDefinition? FindView(ObjectAddress address) => Views.FirstOrDefault(v => v.Address == address);

    /// <summary>
    /// The definition recorded for the routine at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public RoutineDefinition? FindRoutine(ObjectAddress address) => Routines.FirstOrDefault(r => r.Address == address);

    /// <summary>
    /// The definition recorded for the trigger at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public TriggerDefinition? FindTrigger(MemberAddress address) => Triggers.FirstOrDefault(t => t.Address == address);

    /// <summary>
    /// The set restricted to the definitions the scope covers.
    /// </summary>
    public DefinitionSet ScopedTo(PlanningScope scope) => scope.IsUnscoped ? this : new(
        [.. Views.Where(v => scope.Contains(v.Address))],
        [.. Routines.Where(r => scope.Contains(r.Address))],
        [.. Triggers.Where(t => scope.Contains(t.Address))]);

    /// <summary>
    /// The set containing every definition in either set.
    /// </summary>
    public DefinitionSet Union(DefinitionSet other) => new(
        [.. Views.Union(other.Views)],
        [.. Routines.Union(other.Routines)],
        [.. Triggers.Union(other.Triggers)]);

    /// <summary>
    /// The set containing this set's definitions without those in <paramref name="other"/>.
    /// </summary>
    public DefinitionSet Except(DefinitionSet other) => new(
        [.. Views.Except(other.Views)],
        [.. Routines.Except(other.Routines)],
        [.. Triggers.Except(other.Triggers)]);

    /// <summary>
    /// The set containing the definitions identical in both sets.
    /// </summary>
    public DefinitionSet Intersect(DefinitionSet other) => new(
        [.. Views.Intersect(other.Views)],
        [.. Routines.Intersect(other.Routines)],
        [.. Triggers.Intersect(other.Triggers)]);

    /// <summary>
    /// The set restricted to definitions of objects in the identity set; a trigger rides its owning table.
    /// </summary>
    public DefinitionSet RestrictedTo(IdentitySet identities) => new(
        [.. Views.Where(v => identities.ContainsObject(v.Address))],
        [.. Routines.Where(r => identities.ContainsObject(r.Address))],
        [.. Triggers.Where(t => identities.SchemaObjects.Any(o => o.Covers(t.Address)))]);

    /// <summary>
    /// The set restricted to definitions whose address has a definition in <paramref name="other"/>.
    /// </summary>
    public DefinitionSet RestrictedTo(DefinitionSet other) => new(
        [.. Views.Where(v => other.Views.Any(o => o.Address == v.Address))],
        [.. Routines.Where(r => other.Routines.Any(o => o.Address == r.Address))],
        [.. Triggers.Where(t => other.Triggers.Any(o => o.Address == t.Address))]);
}
