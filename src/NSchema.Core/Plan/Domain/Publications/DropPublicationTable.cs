using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents removing a table from a publication.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="Table">The table to remove.</param>
public sealed record DropPublicationTable(SqlIdentifier PublicationName, ObjectAddress Table) : MigrationAction;
