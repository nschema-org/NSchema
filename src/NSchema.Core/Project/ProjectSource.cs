using Microsoft.Extensions.FileSystemGlobbing;

namespace NSchema.Project;

/// <summary>
/// A registered source of project files.
/// </summary>
internal sealed record ProjectSource(string BaseDirectory, Matcher Matcher);
