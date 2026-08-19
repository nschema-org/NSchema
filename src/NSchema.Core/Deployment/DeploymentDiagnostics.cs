namespace NSchema.Deployment;

/// <summary>
/// The diagnostics minted by the live database provider.
/// </summary>
internal static class DeploymentDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Current;

    /// <summary>
    /// An online read without a registered live database provider.
    /// </summary>
    public static Diagnostic NoOnlineSource =>
        Diagnostic.Error(Source, "no-online-source", "No online database provider is registered.");
}
