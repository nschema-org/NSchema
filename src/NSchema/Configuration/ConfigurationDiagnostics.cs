namespace NSchema.Configuration;

/// <summary>
/// The diagnostics the CLI mints while resolving a command's configuration.
/// </summary>
internal static class ConfigurationDiagnostics
{
    internal static readonly DiagnosticSource Source = "config";

    /// <summary>
    /// An <c>--environment</c> no overlay file covers.
    /// </summary>
    public static Diagnostic UnknownEnvironment(string environment) =>
        Diagnostic.Error(Source, "unknown-environment", $"No configuration files found for environment '{environment}'.");

    /// <summary>
    /// A resolved configuration that fails its command's validator.
    /// </summary>
    /// <remarks>
    /// The message is the validator's own, which already names the property it is about.
    /// </remarks>
    public static Diagnostic InvalidConfiguration(string message) =>
        Diagnostic.Error(Source, "invalid-configuration", $"{message:text}");
}
