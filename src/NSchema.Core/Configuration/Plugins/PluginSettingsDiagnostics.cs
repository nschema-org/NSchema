namespace NSchema.Configuration.Plugins;

/// <summary>
/// The diagnostics minted when binding a settings statement onto a plugin's settings type.
/// </summary>
internal static class PluginSettingsDiagnostics
{
    internal static readonly DiagnosticSource Source = "settings";

    /// <summary>
    /// A data-annotation failure on the bound instance.
    /// </summary>
    public static Diagnostic InvalidSetting(string? message) =>
        Diagnostic.Error(Source, "invalid-setting", $"{(message ?? "Invalid configuration."):text}");

    /// <summary>
    /// A setting that matches no property on the settings type.
    /// </summary>
    public static Diagnostic UnknownSetting(string key) =>
        Diagnostic.Error(Source, "unknown-setting", $"Unknown setting '{key}'.");

    /// <summary>
    /// A value that does not fit the property it is written to.
    /// </summary>
    public static Diagnostic UnassignableValue(string? value, string key) =>
        Diagnostic.Error(Source, "unassignable-value", $"Value '{value}' cannot be assigned to '{key}'.");
}
