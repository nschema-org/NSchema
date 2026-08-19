namespace NSchema.Configuration;

/// <summary>
/// One precedence level of configuration — the source files that contribute at that level.
/// When layers are loaded together, a later layer overrides an earlier one.
/// </summary>
/// <param name="Paths">The configuration files at this layer.</param>
public sealed record ConfigurationLayer(IReadOnlyList<string> Paths);
