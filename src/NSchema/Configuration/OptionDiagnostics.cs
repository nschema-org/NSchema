using NSchema.Model;

namespace NSchema.Configuration;

/// <summary>
/// The diagnostics the CLI mints while reading the values given to its own options.
/// </summary>
internal static class OptionDiagnostics
{
    internal static readonly DiagnosticSource Source = "options";

    /// <summary>
    /// A <c>--scope</c> value that does not read as an address.
    /// </summary>
    public static Diagnostic InvalidScope(string value, string reason) =>
        Diagnostic.Error(Source, "invalid-scope",
            $"--scope '{value}': {reason} Name a schema ('app') or an object ('app.orders').");

    /// <summary>
    /// A <c>--scope</c> value addressing a member, which is a level below what a run can target.
    /// </summary>
    public static Diagnostic UnsupportedScopeTarget(string value, Address owner) =>
        Diagnostic.Error(Source, "unsupported-scope-target",
            $"--scope '{value}': scoping to a column or constraint is not supported yet. Scope to '{owner}' instead.");
}
