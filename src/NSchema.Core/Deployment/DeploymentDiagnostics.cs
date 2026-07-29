namespace NSchema.Deployment;

/// <summary>
/// The diagnostics minted by the live database provider.
/// </summary>
internal static class DeploymentDiagnostics
{
    private const string Source = "current";

    /// <summary>
    /// An online read without a registered live database provider.
    /// </summary>
    public static Diagnostic NoOnlineSource =>
        Diagnostic.Error(Source, "No online database provider is registered.");
}
