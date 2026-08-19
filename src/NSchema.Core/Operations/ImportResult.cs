using NSchema.Model;

namespace NSchema.Operations;

/// <summary>
/// The result of an import.
/// </summary>
/// <param name="Database">The live schema read from the database.</param>
/// <param name="WrittenFiles">The full paths of the DDL files written, in write order. Import is additive, so a path
/// here may have been newly created or merged into.</param>
public sealed record ImportResult(Database Database, IReadOnlyList<string> WrittenFiles);
