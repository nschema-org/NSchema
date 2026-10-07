using NSchema.Model;
using NSchema.Model.Publications;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents adding a table to a publication.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="Table">The table's entry, with its columns and filter.</param>
public sealed record AddPublicationTable(SqlIdentifier PublicationName, PublishedTable Table) : MigrationAction;
