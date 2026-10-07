using NSchema.Model;

namespace NSchema.Diff.Domain.Publications;

/// <summary>
/// Describes a schema a publication starts or stops publishing.
/// </summary>
/// <param name="Change"><see cref="ChangeKind.Add"/> when the schema is published, <see cref="ChangeKind.Remove"/> when it no longer is.</param>
/// <param name="Schema">The schema.</param>
public sealed record PublishedSchemaChange(ChangeKind Change, SqlIdentifier Schema);
