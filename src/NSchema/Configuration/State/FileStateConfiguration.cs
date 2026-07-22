using System.ComponentModel.DataAnnotations;
using NSchema.Configuration.Plugins;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a local-file state store.
/// </summary>
internal sealed class FileStateConfiguration
{
    private class FileOptions
    {
        [Required] public string Path { get; set; } = "";
    }

    /// <summary>
    /// The path to the state file.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Maps a <c>STATE file</c> statement's attributes onto a new config, rejecting any it doesn't recognize.
    /// </summary>
    public static FileStateConfiguration FromSettings(PluginSettings config)
    {
        var result = config.Get<FileStateConfiguration>();
        return result.IsFailure
            ? throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)))
            : result.Require();
    }
}
