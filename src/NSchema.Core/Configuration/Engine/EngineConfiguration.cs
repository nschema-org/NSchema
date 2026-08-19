using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Engine;

/// <summary>
/// The engine-level configuration an <c>ENGINE</c> statement declares — a version assertion against the engine,
/// the host tool, or both. Both assertions are optional.
/// </summary>
public sealed record EngineConfiguration
{
    /// <summary>
    /// The engine (Core) version the project requires, or <see langword="null"/> when none is declared.
    /// </summary>
    public VersionRange? Version { get; init; }

    /// <summary>
    /// The host-tool version the project requires, or <see langword="null"/> when none is declared.
    /// </summary>
    public VersionRange? HostVersion { get; init; }
}
