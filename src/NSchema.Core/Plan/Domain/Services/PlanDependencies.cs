using NSchema.Model;
using NSchema.Model.Services;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// What the plan's ordering asks about: which objects have to be in place before another one can be, and which have to go before it can.
/// </summary>
/// <remarks>
/// A migration has two sides, and the direction of the change decides which one answers.
/// What a creation needs is described by the project, which is the only place the new object exists;
/// what a drop costs is recorded in the current database, which is what the statements will run against.
/// </remarks>
internal sealed class PlanDependencies(Database current, Database desired)
{
    private readonly DependencyGraph _current = new(current);
    private readonly DependencyGraph _desired = new(desired);

    /// <summary>
    /// A migration between two empty databases: nothing depends on anything.
    /// </summary>
    public static PlanDependencies None { get; } = new(new Database { Schemas = [] }, new Database { Schemas = [] });

    /// <summary>
    /// The objects that must exist before the one at <paramref name="address"/> can be created.
    /// </summary>
    public IReadOnlyCollection<ObjectAddress> Requires(ObjectAddress address) => _desired.ObjectDependenciesOf(address);

    /// <summary>
    /// The objects that must go before the one at <paramref name="address"/> can be dropped.
    /// </summary>
    public IReadOnlyCollection<ObjectAddress> RequiredBy(ObjectAddress address) => _current.ObjectDependentsOf(address);

    /// <summary>
    /// The foreign keys the current database points at <paramref name="address"/> with.
    /// </summary>
    public IReadOnlyCollection<MemberAddress> ForeignKeysInto(ObjectAddress address) => _current.ForeignKeysInto(address);

    /// <summary>
    /// The triggers the current database carries on the table at <paramref name="address"/>.
    /// </summary>
    public IReadOnlyCollection<MemberAddress> TriggersOn(ObjectAddress address) =>
        [.. current.Objects<Table>()
            .Where(t => t.Schema == address.Schema && t.Object.Name == address.Name)
            .SelectMany(t => t.Object.Triggers.Select(trigger => new MemberAddress(t.Schema, t.Object.Name, trigger.Name)))];
}
