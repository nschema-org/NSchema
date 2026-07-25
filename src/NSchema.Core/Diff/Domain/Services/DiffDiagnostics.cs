using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// The diagnostics minted while computing the diff.
/// </summary>
internal static class DiffDiagnostics
{
    /// <summary>
    /// Objects outside the scope that this run's removals cost, and so must go with them.
    /// </summary>
    public static Diagnostic SeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan reaches outside its scope: {Render(addresses)} depend on objects it removes, so they are removed too. Narrow what you are dropping, or drop these deliberately.");

    /// <summary>
    /// The same, for objects reached only through an edge NSchema inferred rather than one the model states.
    /// </summary>
    public static Diagnostic InferredSeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan also removes {Render(addresses)}, which appear to read something it removes. NSchema does not parse SQL, so it reads view bodies to find this — check these before applying: a body it misread means removing something that did not need to go.");

    /// <summary>
    /// Columns outside the scope that store data typed by something this run removes, which blocks the removal:
    /// a column cannot be severed without destroying its data.
    /// </summary>
    public static Diagnostic ColumnBlocksRemoval(IEnumerable<Address> addresses) => Diagnostic.Error("scope",
        $"This plan removes types that {Render(addresses)} still depend on for stored data. A column cannot be severed the way a constraint or view can — dropping it would destroy rows this run is not about — so the removal is blocked. Migrate those columns off the type first, or keep it declared.");

    /// <summary>
    /// The same, for columns reached only through a bare type name NSchema matched rather than one the model
    /// qualifies — hedged, because a wrong match would block a plan that need not be.
    /// </summary>
    public static Diagnostic InferredColumnMayBlockRemoval(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"{Render(addresses)} appear to be typed by something this plan removes. The type name is unqualified, so NSchema matched it by name alone — if the match is right, the database will reject the removal at apply. Check before applying: migrate those columns off the type first, or keep it declared.");

    /// <summary>
    /// Foreign keys this run adds whose target it will neither create nor find, so they are left out rather
    /// than emitted as a plan the database would reject.
    /// </summary>
    public static Diagnostic ForeignKeyTargetOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan leaves out {Render(addresses)}: each references a table outside its scope that does not exist yet, so creating the constraint would fail. The tables are created without them — widen the scope to include the referenced tables, then re-plan to add the constraints.");

    /// <summary>
    /// Additions naming a type this run will neither create nor find. A type is part of the object's shape, so
    /// unlike a constraint there is nothing to leave out — the plan is blocked instead.
    /// </summary>
    public static Diagnostic TypeTargetOutOfScope(IEnumerable<Address> dependents, IEnumerable<Address> types) => Diagnostic.Error("scope",
        $"This plan cannot be applied: {Render(dependents)} are typed by {Render(types)}, which it does not create and the database does not have. A type cannot be left out the way a constraint can — it is part of the object's shape — so the plan is blocked. Widen the scope to include those types, then re-plan.");

    private static string Render(IEnumerable<Address> addresses) =>
        string.Join(", ", addresses.Select(a => $"'{a}'"));

    /// <summary>
    /// A run-once script whose body has changed since its recorded execution; it stays skipped.
    /// </summary>
    public static Diagnostic ChangedRunOnceScript(DeploymentScript script) => Diagnostic.Warning("run-once",
        $"Run-once script '{script.Address}' has changed since it was executed and stays skipped.");

    /// <summary>
    /// A change-event script that matches nothing in this plan and will not run.
    /// </summary>
    public static Diagnostic DeadMigration(ChangeScript migration) => Diagnostic.Info("data-migrations",
        $"Migration '{migration.Address}' ({migration.Description}) matches no change in this plan and will not run. If the change it supports has been applied everywhere, the block is safe to delete.");

    /// <summary>
    /// A rename directive whose source is gone and whose target already exists.
    /// </summary>
    public static Diagnostic AppliedRename(string kind, string address, SqlIdentifier to) => Diagnostic.Info("directives",
        $"Rename of {kind:text} '{address}' matches nothing in the current state and '{to}' already exists — the rename has been applied. Once it has been applied everywhere, the directive is safe to delete.");

    /// <summary>
    /// A rename whose previous name is still declared, which is indistinguishable from a retain-plus-create.
    /// </summary>
    public static Diagnostic AmbiguousRenameSourceStillDeclared(string kind, string address, SqlIdentifier from) => Diagnostic.Error("directives",
        $"Ambiguous rename of {kind:text} '{address}' from '{from}': its previous name '{from}' is still declared. Perform the rename and the conflicting change in separate migrations.");

    /// <summary>
    /// A rename whose new name is already taken by another current entity.
    /// </summary>
    public static Diagnostic AmbiguousRenameTargetTaken(string kind, string address, SqlIdentifier from, SqlIdentifier to) => Diagnostic.Error("directives",
        $"Ambiguous rename of {kind:text} '{address}' from '{from}': a {kind:text} named '{to}' already exists. Perform the rename and the conflicting change in separate migrations.");
}
