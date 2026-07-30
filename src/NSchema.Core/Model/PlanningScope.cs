namespace NSchema.Model;

/// <summary>
/// What part of the database a run is about.
/// </summary>
/// <remarks>
/// An entry covers itself and everything below it, but never drags its container's concerns into the run.
/// </remarks>
public sealed record PlanningScope
{
    private PlanningScope(IReadOnlyList<Address> addresses) => Addresses = addresses;

    /// <summary>
    /// Every schema the source can see.
    /// </summary>
    public static PlanningScope All { get; } = new([]);

    /// <summary>
    /// A scope covering the given addresses. An empty set normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="addresses">The addresses under management.</param>
    public static PlanningScope To(params IEnumerable<Address> addresses)
    {
        var list = addresses.Distinct().ToList();
        // An address already covered by a broader entry is redundant.
        var kept = list.Where(a => !list.Any(b => !b.Equals(a) && b.Covers(a))).ToList();
        return kept.Count == 0 ? All : new PlanningScope(kept);
    }

    /// <summary>
    /// The addresses the scope covers.
    /// </summary>
    public IReadOnlyList<Address> Addresses { get; }

    /// <summary>
    /// Whether this scope covers everything — it holds no addresses to narrow the run.
    /// </summary>
    public bool IsUnscoped => Addresses.Count == 0;

    /// <summary>
    /// Whether the scope covers the given address — an entry equals it or contains it.
    /// </summary>
    public bool Contains(Address address) => IsUnscoped || Addresses.Any(a => a.Covers(address));

    /// <summary>
    /// Whether the scope covers the named schema and everything inside it.
    /// </summary>
    public bool Contains(SqlIdentifier schemaName) => Contains(DatabaseAddress.Schema(schemaName));
}
