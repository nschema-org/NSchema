using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Adapts a parsed <see cref="SettingsStatement"/> to the <see cref="PluginSettings"/> its label and settings describe.
/// </summary>
internal static class SettingsStatementExtensions
{
    /// <summary>
    /// The statement as a <see cref="PluginSettings"/> — its label plus its settings as a flat, case-insensitive map.
    /// </summary>
    public static PluginSettings ToSettings(this SettingsStatement statement) =>
        new(statement.Label?.Value, statement.Settings.ToDictionary(a => a.Key, string? (a) => a.Value, StringComparer.OrdinalIgnoreCase));
}
