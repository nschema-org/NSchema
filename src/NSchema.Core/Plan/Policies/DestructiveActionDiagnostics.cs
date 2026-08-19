namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="DestructiveActionPolicy"/>.
/// </summary>
internal static class DestructiveActionDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.DestructiveActions;

    /// <summary>
    /// Changes in the plan that destroy something: a definition, its data, or a guarantee over it.
    /// </summary>
    public static Diagnostic DestructiveChange(string actions) =>
        Diagnostic.Error(Source, "destructive-change", $"This plan contains destructive actions: {actions}.")
            with
        { Kind = DiagnosticKind.Advisory };
}
