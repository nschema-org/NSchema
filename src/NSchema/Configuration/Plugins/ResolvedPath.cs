namespace NSchema.Configuration.Plugins;

/// <summary>
/// A built assembly, at a path already made absolute against the project root.
/// </summary>
internal sealed record ResolvedPath(string AssemblyPath) : ResolvedOrigin;
