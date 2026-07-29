using NSchema.Model;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// The diagnostics minted by the planner.
/// </summary>
internal static class PlanDiagnostics
{
    private const string Source = "plan";

    /// <summary>
    /// Planning without a registered SQL dialect (the plan's statements cannot be rendered).
    /// </summary>
    public static Diagnostic MissingDialect => Diagnostic.Error(Source, "Planning requires a database provider to render SQL, but none is registered.");

    /// <summary>
    /// A declared object matches an observed one with the same name, but different casing.
    /// </summary>
    public static Diagnostic CaseOnlyMismatch(Address declared, Address observed) =>
        Diagnostic.Warning(Source, $"The project declares schema '{declared}' but the database has '{observed}', which differs only by case.");

    /// <summary>
    /// Schemas the plan creates objects in that it will neither create nor find, because nothing declares them.
    /// </summary>
    public static Diagnostic UndeclaredSchemaMissing(IEnumerable<SqlIdentifier> schemas)
    {
        var names = schemas.ToList();
        var subject = names.Count == 1
            ? $"schema '{names[0]}'"
            : $"schemas {string.Join(", ", names.Select(name => $"'{name}'"))}";
        var pronoun = names.Count == 1 ? "it" : "them";

        return Diagnostic.Error(Source,
            $"This plan creates objects in {subject}, which could not be found in the project or state.\n"
            + $"Declare {pronoun} with CREATE SCHEMA or refresh the state if the database has it already."
        );
    }
}
