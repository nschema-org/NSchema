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
    /// The check constraint definitions in the set.
    /// </summary>
    public IReadOnlyList<CheckConstraintDefinition> Checks { get; init; } = [];

    /// <summary>
    /// The column expression definitions in the set.
    /// </summary>
    public IReadOnlyList<ColumnExpressionDefinition> Columns { get; init; } = [];

    /// <summary>
    /// The index definitions in the set.
    /// </summary>
    public IReadOnlyList<IndexPredicateDefinition> Indexes { get; init; } = [];

    /// <summary>
    /// The exclusion constraint definitions in the set.
    /// </summary>
    public IReadOnlyList<ExclusionConstraintDefinition> Exclusions { get; init; } = [];

    /// <summary>
    /// The domain definitions in the set.
    /// </summary>
    public IReadOnlyList<DomainDefinition> Domains { get; init; } = [];

    /// <summary>
    /// Whether the set contains no definitions.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Views.Count == 0 && Routines.Count == 0 && Triggers.Count == 0 && Checks.Count == 0
        && Columns.Count == 0 && Indexes.Count == 0 && Exclusions.Count == 0 && Domains.Count == 0;

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
    /// The definition recorded for the check constraint at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public CheckConstraintDefinition? FindCheck(MemberAddress address) => Checks.FirstOrDefault(c => c.Address == address);

    /// <summary>
    /// The definition recorded for the column at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public ColumnExpressionDefinition? FindColumn(MemberAddress address) => Columns.FirstOrDefault(c => c.Address == address);

    /// <summary>
    /// The definition recorded for the index at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public IndexPredicateDefinition? FindIndex(MemberAddress address) => Indexes.FirstOrDefault(i => i.Address == address);

    /// <summary>
    /// The definition recorded for the exclusion constraint at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public ExclusionConstraintDefinition? FindExclusion(MemberAddress address) => Exclusions.FirstOrDefault(e => e.Address == address);

    /// <summary>
    /// The definition recorded for the domain at <paramref name="address"/>, or <see langword="null"/> when none.
    /// </summary>
    public DomainDefinition? FindDomain(ObjectAddress address) => Domains.FirstOrDefault(d => d.Address == address);

    /// <summary>
    /// The set restricted to the definitions the scope covers.
    /// </summary>
    public DefinitionSet ScopedTo(PlanningScope scope) => scope.IsUnscoped ? this : new(
        [.. Views.Where(v => scope.Contains(v.Address))],
        [.. Routines.Where(r => scope.Contains(r.Address))],
        [.. Triggers.Where(t => scope.Contains(t.Address))])
    {
        Checks = [.. Checks.Where(c => scope.Contains(c.Address))],
        Columns = [.. Columns.Where(c => scope.Contains(c.Address))],
        Indexes = [.. Indexes.Where(i => scope.Contains(i.Address))],
        Exclusions = [.. Exclusions.Where(e => scope.Contains(e.Address))],
        Domains = [.. Domains.Where(d => scope.Contains(d.Address))],
    };

    /// <summary>
    /// The set containing every definition in either set.
    /// </summary>
    public DefinitionSet Union(DefinitionSet other) => new(
        [.. Views.Union(other.Views)],
        [.. Routines.Union(other.Routines)],
        [.. Triggers.Union(other.Triggers)])
    {
        Checks = [.. Checks.Union(other.Checks)],
        Columns = [.. Columns.Union(other.Columns)],
        Indexes = [.. Indexes.Union(other.Indexes)],
        Exclusions = [.. Exclusions.Union(other.Exclusions)],
        Domains = [.. Domains.Union(other.Domains)],
    };

    /// <summary>
    /// The set containing this set's definitions without those in <paramref name="other"/>.
    /// </summary>
    public DefinitionSet Except(DefinitionSet other) => new(
        [.. Views.Except(other.Views)],
        [.. Routines.Except(other.Routines)],
        [.. Triggers.Except(other.Triggers)])
    {
        Checks = [.. Checks.Except(other.Checks)],
        Columns = [.. Columns.Except(other.Columns)],
        Indexes = [.. Indexes.Except(other.Indexes)],
        Exclusions = [.. Exclusions.Except(other.Exclusions)],
        Domains = [.. Domains.Except(other.Domains)],
    };

    /// <summary>
    /// The set containing the definitions identical in both sets.
    /// </summary>
    public DefinitionSet Intersect(DefinitionSet other) => new(
        [.. Views.Intersect(other.Views)],
        [.. Routines.Intersect(other.Routines)],
        [.. Triggers.Intersect(other.Triggers)])
    {
        Checks = [.. Checks.Intersect(other.Checks)],
        Columns = [.. Columns.Intersect(other.Columns)],
        Indexes = [.. Indexes.Intersect(other.Indexes)],
        Exclusions = [.. Exclusions.Intersect(other.Exclusions)],
        Domains = [.. Domains.Intersect(other.Domains)],
    };

    /// <summary>
    /// The set restricted to definitions of objects in the identity set; a trigger or check rides its owning table.
    /// </summary>
    public DefinitionSet RestrictedTo(IdentitySet identities) => new(
        [.. Views.Where(v => identities.ContainsObject(v.Address))],
        [.. Routines.Where(r => identities.ContainsObject(r.Address))],
        [.. Triggers.Where(t => identities.SchemaObjects.Any(o => o.Covers(t.Address)))])
    {
        Checks = [.. Checks.Where(c => identities.SchemaObjects.Any(o => o.Covers(c.Address)))],
        Columns = [.. Columns.Where(c => identities.SchemaObjects.Any(o => o.Covers(c.Address)))],
        Indexes = [.. Indexes.Where(i => identities.SchemaObjects.Any(o => o.Covers(i.Address)))],
        Exclusions = [.. Exclusions.Where(e => identities.SchemaObjects.Any(o => o.Covers(e.Address)))],
        Domains = [.. Domains.Where(d => identities.ContainsObject(d.Address))],
    };

    /// <summary>
    /// The set restricted to definitions whose address has a definition in <paramref name="other"/>.
    /// </summary>
    public DefinitionSet RestrictedTo(DefinitionSet other) => new(
        [.. Views.Where(v => other.Views.Any(o => o.Address == v.Address))],
        [.. Routines.Where(r => other.Routines.Any(o => o.Address == r.Address))],
        [.. Triggers.Where(t => other.Triggers.Any(o => o.Address == t.Address))])
    {
        Checks = [.. Checks.Where(c => other.Checks.Any(o => o.Address == c.Address))],
        Columns = [.. Columns.Where(c => other.Columns.Any(o => o.Address == c.Address))],
        Indexes = [.. Indexes.Where(i => other.Indexes.Any(o => o.Address == i.Address))],
        Exclusions = [.. Exclusions.Where(e => other.Exclusions.Any(o => o.Address == e.Address))],
        Domains = [.. Domains.Where(d => other.Domains.Any(o => o.Address == d.Address))],
    };
}
