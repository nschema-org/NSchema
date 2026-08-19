using System.Collections.Frozen;

namespace NSchema.Diagnostics;

/// <summary>
/// Every source the engine reports a finding under.
/// </summary>
/// <remarks>
/// A source names a producer in configuration, so it is a contract — and one producer is often several classes,
/// which is what makes a literal per class drift. Declaring them here is what keeps the classes sharing a source
/// agreeing on it, and what lets a caller check a configured name against the sources that exist.
/// </remarks>
public static class DiagnosticSources
{
    /// <summary>Objects taken under management by a plan.</summary>
    public static readonly DiagnosticSource Adoption = "adoption";

    /// <summary>Executing a migration plan.</summary>
    public static readonly DiagnosticSource Apply = "apply";

    /// <summary>A declaration the target engine cannot honor.</summary>
    public static readonly DiagnosticSource Capability = "capability";

    /// <summary>Assembling the project's configuration.</summary>
    public static readonly DiagnosticSource Config = "config";

    /// <summary>Reading the live database.</summary>
    public static readonly DiagnosticSource Current = "current";

    /// <summary>A change that puts existing data at risk.</summary>
    public static readonly DiagnosticSource DataHazards = "data-hazards";

    /// <summary>A change that discards a database object.</summary>
    public static readonly DiagnosticSource DestructiveActions = "destructive-actions";

    /// <summary>Comparing the current schema against the desired one.</summary>
    public static readonly DiagnosticSource Diff = "diff";

    /// <summary>The infrastructure health checks.</summary>
    public static readonly DiagnosticSource Doctor = "doctor";

    /// <summary>Applying the configured enforcement to a finding.</summary>
    public static readonly DiagnosticSource Enforcement = "enforcement";

    /// <summary>An enum change that cannot be planned as an alteration.</summary>
    public static readonly DiagnosticSource EnumValueRemoval = "enum-value-removal";

    /// <summary>The canonical layout of a statement.</summary>
    public static readonly DiagnosticSource Formatting = "formatting";

    /// <summary>Taking and releasing the state lock.</summary>
    public static readonly DiagnosticSource Lock = "lock";

    /// <summary>The plugin lockfile.</summary>
    public static readonly DiagnosticSource LockFile = "lockfile";

    /// <summary>Computing a migration plan.</summary>
    public static readonly DiagnosticSource Plan = "plan";

    /// <summary>Reading a saved plan file.</summary>
    public static readonly DiagnosticSource PlanFile = "plan-file";

    /// <summary>Declaring, resolving and handshaking plugins.</summary>
    public static readonly DiagnosticSource Plugins = "plugins";

    /// <summary>Loading the declared project.</summary>
    public static readonly DiagnosticSource Project = "project";

    /// <summary>Capturing the live schema to the state store.</summary>
    public static readonly DiagnosticSource Refresh = "refresh";

    /// <summary>Schema conventions a project is held to.</summary>
    public static readonly DiagnosticSource SchemaLint = "schema-lint";

    /// <summary>Binding a plugin's settings.</summary>
    public static readonly DiagnosticSource Settings = "settings";

    /// <summary>Rendering an action through the dialect.</summary>
    public static readonly DiagnosticSource SqlDialect = "sql-dialect";

    /// <summary>Documentation an engine cannot keep.</summary>
    public static readonly DiagnosticSource Comments = "comments";

    /// <summary>Reading and writing the recorded state.</summary>
    public static readonly DiagnosticSource State = "state";

    /// <summary>A project that does not hold together.</summary>
    public static readonly DiagnosticSource StructuralIntegrity = "structural-integrity";

    /// <summary>Lexing and parsing NSQL source.</summary>
    public static readonly DiagnosticSource Syntax = "syntax";

    /// <summary>Adopting members onto a table.</summary>
    public static readonly DiagnosticSource Table = "table";

    /// <summary>Applying reusable table and column declarations.</summary>
    public static readonly DiagnosticSource Templates = "templates";

    /// <summary>
    /// Every source above, for a caller checking a configured name against the sources that exist.
    /// </summary>
    /// <remarks>
    /// The engine's own sources: a plugin or a host reports under sources of its own, which this does not know.
    /// </remarks>
    public static IReadOnlySet<DiagnosticSource> All { get; } = FrozenSet.ToFrozenSet(
    [
        Adoption, Apply, Capability, Comments, Config, Current, DataHazards, DestructiveActions, Diff, Doctor, Enforcement,
        EnumValueRemoval, Formatting, Lock, LockFile, Plan, PlanFile, Plugins, Project, Refresh, SchemaLint,
        Settings, SqlDialect, State, StructuralIntegrity, Syntax, Table, Templates,
    ]);
}
