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
        $"This plan will remove objects outside its scope: {Render(addresses)} depend on objects it removes, so they are removed too.");

    /// <summary>
    /// The same, for objects reached only through an edge NSchema inferred rather than one the model states.
    /// </summary>
    public static Diagnostic InferredSeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan also removes {Render(addresses)}, which appear to depend on something it removes.");

    /// <summary>
    /// Columns outside the scope that store data typed by something this run removes, which blocks the removal:
    /// a column cannot be severed without destroying its data.
    /// </summary>
    public static Diagnostic ColumnBlocksRemoval(IEnumerable<Address> addresses) => Diagnostic.Error("scope",
        $"{Render(addresses)} depend on one or more types this plan removes.");

    /// <summary>
    /// The same, for columns reached only through a bare type name NSchema matched rather than one the model qualifies.
    /// </summary>
    public static Diagnostic InferredColumnMayBlockRemoval(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"{Render(addresses)} appear to depend on one or more types this plan removes.");

    /// <summary>
    /// Foreign keys this run adds whose target it will neither create nor find.
    /// </summary>
    public static Diagnostic ForeignKeyTargetOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"{Render(addresses)} reference tables that do not exist yet, so the constraints are not included in the plan.");

    /// <summary>
    /// Additions naming a type this run will neither create nor find. A type is part of the object's shape, so
    /// unlike a constraint there is nothing to leave out — the plan is blocked instead.
    /// </summary>
    public static Diagnostic TypeTargetOutOfScope(IEnumerable<Address> dependents, IEnumerable<Address> types) => Diagnostic.Error("scope",
        $"{Render(dependents)} depends on {Render(types)}, which it does not create and the database does not have.");

    private static string Render(IEnumerable<Address> addresses) =>
        string.Join(", ", addresses.Select(a => $"'{a}'"));

    /// <summary>
    /// A run-once script whose body has changed since its recorded execution; it stays skipped.
    /// </summary>
    public static Diagnostic ChangedRunOnceScript(DeploymentScript script) => Diagnostic.Warning("run-once",
        $"Script '{script.Address}' has changed since it was executed and stays skipped.");

    /// <summary>
    /// A change-event script that matches nothing in this plan and will not run.
    /// </summary>
    public static Diagnostic DeadMigration(ChangeScript migration) => Diagnostic.Info("data-migrations",
        $"Migration '{migration.Address}' ({migration.Description}) matches no change in this plan.");

    /// <summary>
    /// A rename directive whose source is gone and whose target already exists.
    /// </summary>
    public static Diagnostic AppliedRename(string kind, Address address, SqlIdentifier to) => Diagnostic.Info("directives",
        $"The {kind:text} '{address}' has already been renamed to '{to}'.");

    /// <summary>
    /// A rename whose previous name is still declared, which is indistinguishable from a retain-plus-create.
    /// </summary>
    public static Diagnostic AmbiguousRenameSourceStillDeclared(string kind, Address address, SqlIdentifier from) => Diagnostic.Error("directives",
        $"Unable to rename {kind:text} '{address}'. An object bearing the old name '{from}' is still declared.");

    /// <summary>
    /// A rename whose new name is already taken by another current entity.
    /// </summary>
    public static Diagnostic AmbiguousRenameTargetTaken(string kind, Address address, SqlIdentifier from, SqlIdentifier to) => Diagnostic.Error("directives",
        $"Unable to rename {kind:text} '{address}' from '{from}': a {kind:text} named '{to}' already exists.");
}
