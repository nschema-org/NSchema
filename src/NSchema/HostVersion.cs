using NSchema.Configuration.Model;

namespace NSchema;

/// <summary>
/// The version of this CLI tool — the engine an <c>ENGINE</c> assertion refers to. Read from the CLI's own
/// assembly, never Core's: a user pins the tool they installed and cannot see the Core version behind it.
/// </summary>
internal static class HostVersion
{
    /// <summary>
    /// This tool's version, with any prerelease dropped (ENGINE ranges are release-oriented).
    /// </summary>
    public static SemanticVersion Current { get; } = Read();

    private static SemanticVersion Read()
    {
        var version = typeof(HostVersion).Assembly.GetName().Version!;
        return new SemanticVersion(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0), Prerelease: null);
    }
}
