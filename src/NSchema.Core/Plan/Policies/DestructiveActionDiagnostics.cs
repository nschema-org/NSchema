namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="DestructiveActionPolicy"/>.
/// </summary>
internal static class DestructiveActionDiagnostics
{
    private const string Source = "destructive-actions";

    /// <summary>
    /// Destructive actions the configured policy permits.
    /// </summary>
    public static Diagnostic Allowed(string actions) =>
        Diagnostic.Info(Source, $"Allowing destructive actions in migration plan: {actions}.");

    /// <summary>
    /// Destructive actions the configured policy permits but flags.
    /// </summary>
    public static Diagnostic Warned(string actions) =>
        Diagnostic.Warning(Source, $"Migration plan contains destructive actions: {actions}.");

    /// <summary>
    /// Destructive actions the configured policy rejects.
    /// </summary>
    public static Diagnostic Blocked(string actions) =>
        Diagnostic.Error(Source, $"Destructive actions blocked by policy: {actions}.");
}
