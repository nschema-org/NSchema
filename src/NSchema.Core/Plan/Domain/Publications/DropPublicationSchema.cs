using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents no longer publishing a schema's tables.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="Schema">The schema to stop publishing.</param>
public sealed record DropPublicationSchema(SqlIdentifier PublicationName, SqlIdentifier Schema) : MigrationAction;
