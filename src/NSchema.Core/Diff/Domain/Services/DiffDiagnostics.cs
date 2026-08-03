using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// The diagnostics minted while computing the diff.
/// </summary>
internal static class DiffDiagnostics
{
    internal static readonly DiagnosticSource Source = "diff";

    /// <summary>
    /// Objects outside the scope that this run's removals cost, and so must go with them.
    /// </summary>
    public static Diagnostic SeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning(Source, "severed-out-of-scope",
        $"{Render(addresses)} depend on objects this plan removes, so they will be removed too.");

    /// <summary>
    /// The same, for objects reached only through an edge NSchema inferred rather than one the model states.
    /// </summary>
    public static Diagnostic InferredSeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning(Source, "inferred-severed-out-of-scope",
        $"{Render(addresses)}, appear to depend on something this plan removes, so they will be removed too.");

    /// <summary>
    /// Columns outside the scope that store data typed by something this run removes, which blocks the removal:
    /// a column cannot be severed without destroying its data.
    /// </summary>
    public static Diagnostic ColumnBlocksRemoval(IEnumerable<Address> addresses) => Diagnostic.Error(Source, "column-blocks-removal",
        $"{Render(addresses)} depend on one or more types this plan removes.");

    /// <summary>
    /// The same, for columns reached only through a bare type name NSchema matched rather than one the model qualifies.
    /// </summary>
    public static Diagnostic InferredColumnMayBlockRemoval(IEnumerable<Address> addresses) => Diagnostic.Warning(Source, "inferred-column-may-block-removal",
        $"{Render(addresses)} appear to depend on one or more types this plan removes.");

    /// <summary>
    /// Foreign keys this run adds whose target it will neither create nor find.
    /// </summary>
    public static Diagnostic ForeignKeyTargetOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning(Source, "foreign-key-target-out-of-scope",
        $"{Render(addresses)} reference tables that do not exist yet, so the constraints are not included in the plan.");

    /// <summary>
    /// References naming a type this run will neither create nor find.
    /// </summary>
    public static Diagnostic UnresolvedTypes(IEnumerable<Address> dependents, IEnumerable<string> types) => Diagnostic.Error(Source, "unresolved-type",
        $"{Render(dependents)} depends on {RenderNames(types)}, which this plan does not create and the database does not have.");

    /// <summary>
    /// References naming a type the plan itself removes.
    /// </summary>
    public static Diagnostic RemovedTypeStillReferenced(IEnumerable<Address> dependents, IEnumerable<string> types) => Diagnostic.Error(Source, "removed-type-still-referenced",
        $"{Render(dependents)} depends on {RenderNames(types)}, which this plan removes.");

    /// <summary>
    /// The same, where the type may come from an extension this run installs.
    /// </summary>
    public static Diagnostic TypeMayComeFromExtension(IEnumerable<Address> dependents, IEnumerable<string> types, IEnumerable<SqlIdentifier> extensions) => Diagnostic.Warning(Source, "unresolved-type-extension-installing",
        $"{Render(dependents)} depends on {RenderNames(types)}, which the database does not have.\n" +
        $"If these pending extensions contain it: {RenderNames(extensions.Select(e => e.Value))}, you may ignore this warning.");

    /// <summary>
    /// The same, where the type namespace was never captured, so absence proves nothing.
    /// </summary>
    public static Diagnostic UnverifiedTypes(IEnumerable<Address> dependents, IEnumerable<string> types) => Diagnostic.Warning(Source, "unverified-type",
        $"{Render(dependents)} depends on {RenderNames(types)}, which cannot be verified to exist.");

    private static string Render(IEnumerable<Address> addresses) =>
        string.Join(", ", addresses.Select(a => $"'{a}'"));

    private static string RenderNames(IEnumerable<string> names) =>
        string.Join(", ", names.Select(n => $"'{n}'"));

    /// <summary>
    /// A run-once script whose body has changed since its recorded execution; it stays skipped.
    /// </summary>
    public static Diagnostic ChangedRunOnceScript(DeploymentScript script) => Diagnostic.Warning(Source, "changed-run-once-script",
        $"Script '{script.Reference}' has changed since it was executed and stays skipped.");

    /// <summary>
    /// A change-event script that matches nothing in this plan and will not run.
    /// </summary>
    public static Diagnostic DeadMigration(ChangeScript migration) => Diagnostic.Info(Source, "dead-migration",
        $"Migration '{migration.Reference}' ({migration.Description}) matches no change in this plan.");

    /// <summary>
    /// A rename directive whose source is gone and whose target already exists.
    /// </summary>
    public static Diagnostic AppliedRename(string kind, Address address, SqlIdentifier to) => Diagnostic.Info(Source, "applied-rename",
        $"The {kind:text} '{address}' has already been renamed to '{to}'.");

    /// <summary>
    /// A rename whose previous name is still declared, which is indistinguishable from a retain-plus-create.
    /// </summary>
    public static Diagnostic AmbiguousRenameSourceStillDeclared(string kind, Address address, SqlIdentifier from) => Diagnostic.Error(Source, "ambiguous-rename-source-still-declared",
        $"Unable to rename {kind:text} '{address}'. An object bearing the old name '{from}' is still declared.");

    /// <summary>
    /// A rename whose new name is already taken by another current entity.
    /// </summary>
    public static Diagnostic AmbiguousRenameTargetTaken(string kind, Address address, SqlIdentifier from, SqlIdentifier to) => Diagnostic.Error(Source, "ambiguous-rename-target-taken",
        $"Unable to rename {kind:text} '{address}' from '{from}': a {kind:text} named '{to}' already exists.");
}
