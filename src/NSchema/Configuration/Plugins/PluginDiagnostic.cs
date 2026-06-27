namespace NSchema.Configuration.Plugins;

/// <summary>
/// Details of a plugin that failed to restore or configure.
/// </summary>
/// <param name="Label">The configuration-block label of the offending plugin (e.g. <c>postgres</c>, <c>s3</c>).</param>
/// <param name="Errors">The failure messages.</param>
internal sealed record PluginDiagnostic(string Label, IReadOnlyList<string> Errors);
