using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents publishing every table in a schema.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="Schema">The schema to publish.</param>
public sealed record AddPublicationSchema(SqlIdentifier PublicationName, SqlIdentifier Schema) : MigrationAction;
