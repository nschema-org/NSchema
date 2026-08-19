using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.State.Domain;

/// <summary>
/// Represents the recorded state of a deployed database: the captured schema snapshot, the run-once script executions, and the identities NSchema manages.
/// </summary>
/// <param name="Database">The full captured database structure.</param>
/// <param name="Scripts">The recorded script executions.</param>
/// <param name="Managed">The identities that we are responsible for managing.</param>
/// <param name="Declared">The declared spellings recorded for the managed body-bearing objects.</param>
public sealed record DatabaseState(
    Database Database,
    IReadOnlyList<ScriptExecution> Scripts,
    IdentitySet? Managed = null,
    DefinitionSet? Declared = null
)
{
    /// <summary>
    /// Creates a state carrying only the database structure, with an empty execution ledger and nothing managed.
    /// </summary>
    public DatabaseState(Database database) : this(database, []) { }

    /// <summary>
    /// The state before anything has been recorded.
    /// </summary>
    public static DatabaseState Empty { get; } = new(new Database());

    /// <summary>
    /// The identities we manage.
    /// </summary>
    public IdentitySet Managed { get; init; } = Managed ?? IdentitySet.Empty;

    /// <summary>
    /// The hand-written declarations recorded for the managed body-bearing objects.
    /// </summary>
    public DefinitionSet Declared { get; init; } = Declared ?? DefinitionSet.Empty;

    /// <summary>
    /// Restricts the recorded state to what the scope covers.
    /// </summary>
    /// <param name="scope">The scope to restrict the state to.</param>
    public DatabaseState ScopedTo(PlanningScope scope) => scope.IsUnscoped ? this : this with
    {
        Database = Database.ScopedTo(scope),
        Scripts = [.. Scripts.Where(e => e.Script.Schema is not { } schema || scope.Contains(schema))],
        Managed = Managed.ScopedTo(scope),
        Declared = Declared.ScopedTo(scope),
    };

    /// <summary>
    /// Replaces the snapshot with a fresh capture. A declared spelling is kept only while the engine
    /// re-rendered its object identically — anything else drifted out of band, so the spelling is stale.
    /// </summary>
    /// <param name="captured">The freshly captured database structure.</param>
    public DatabaseState Recapture(Database captured) => Declared.IsEmpty
        ? this with { Database = captured }
        : this with
        {
            Database = captured,
            Declared = Declared.RestrictedTo(Database.Definitions().Intersect(captured.Definitions())),
        };

    /// <summary>
    /// Records the given executions into the ledger, replacing any earlier execution recorded for the same script.
    /// </summary>
    /// <param name="executions">The executions to record.</param>
    public DatabaseState RecordExecution(IReadOnlyList<ScriptExecution> executions)
    {
        if (executions.Count == 0)
        {
            return this;
        }

        var merged = Scripts
            .Where(e => executions.All(s => s.Script != e.Script))
            .Concat(executions)
            .ToList();
        return this with { Scripts = merged };
    }

    /// <summary>
    /// Records the run-once entries implied by a set of applied deployment scripts, keyed by address and body
    /// hash. The ledger is a deployment-script concern — a change-event script runs whenever its change is
    /// planned, gated by the diff itself, so it is never recorded.
    /// </summary>
    /// <param name="applied">The deployment scripts that ran.</param>
    /// <param name="executedAt">When they ran.</param>
    public DatabaseState RecordExecution(IReadOnlyList<DeploymentScript> applied, DateTimeOffset executedAt) =>
        RecordExecution([.. applied
            .Where(s => s.RunCondition == RunCondition.Once)
            .Select(s => new ScriptExecution(s.Reference, s.Hash, executedAt))]);

    /// <summary>
    /// Finds the recorded execution for the given script, or <see langword="null"/> when none is recorded.
    /// </summary>
    /// <param name="script">The script's address.</param>
    public ScriptExecution? FindExecution(ScriptReference script) => Scripts.FirstOrDefault(e => e.Script == script);

    /// <summary>
    /// Removes the recorded execution for the given script, so a later plan runs the script again.
    /// </summary>
    /// <param name="script">The script's address.</param>
    public DatabaseState RemoveExecution(ScriptReference script)
    {
        var executions = Scripts.Where(e => e.Script != script).ToList();
        return executions.Count == Scripts.Count ? this : this with { Scripts = executions };
    }
}
