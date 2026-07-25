using System.Collections;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Applies environment overrides to a configuring statement's settings, so a secret never has to be written into a project file.
/// </summary>
/// <remarks>
/// A variable is named <c>NSCHEMA_&lt;KEYWORD&gt;_&lt;SETTING&gt;</c> — <c>NSCHEMA_DATABASE_CONNECTION_STRING</c> sets
/// <c>connection_string</c> on the <c>DATABASE</c> statement. The keyword is part of the name because a project has
/// both a <c>DATABASE</c> and a <c>STATE</c> statement, and a backend for either may want a setting of the same name.
/// <para>
/// This is one rule for every setting rather than a list a plugin must maintain: a plugin declares its options and
/// they become overridable, so nothing has to be taught about each new one.
/// </para>
/// </remarks>
internal static class EnvironmentSettings
{
    /// <summary>
    /// <paramref name="settings"/> with any environment override applied. An override replaces the written value,
    /// and may supply a setting the statement omits entirely.
    /// </summary>
    /// <param name="settings">The settings as written in the statement.</param>
    /// <param name="keyword">The statement's keyword, which scopes the variable names read.</param>
    /// <param name="environment">The variables to read; the process environment when not supplied.</param>
    public static PluginSettings Overlay(PluginSettings settings, string keyword, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var prefix = $"NSCHEMA_{keyword.ToUpperInvariant()}_";
        var values = new Dictionary<string, string?>(settings.Values, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in environment ?? Process())
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                values[name[prefix.Length..].ToLowerInvariant()] = value;
            }
        }

        return settings with { Values = values };
    }

    /// <summary>
    /// The process environment, as a case-insensitive map.
    /// </summary>
    private static Dictionary<string, string?> Process()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            values[(string)entry.Key] = entry.Value as string;
        }

        return values;
    }
}
