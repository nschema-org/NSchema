namespace NSchema.Model;

/// <summary>
/// The declared spelling of a published table's row filter.
/// </summary>
/// <param name="Publication">The publication's name.</param>
/// <param name="Table">The published table.</param>
/// <param name="Filter">The declared filter text, where the entry has one.</param>
public sealed record PublishedTableDefinition(
    SqlIdentifier Publication,
    ObjectAddress Table,
    SqlText? Filter = null
);
