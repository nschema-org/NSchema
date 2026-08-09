using NSchema.Model;
using NSchema.Model.XmlSchemaCollections;

namespace NSchema.Plan.Domain.XmlSchemaCollections;

/// <summary>
/// Represents creating an XML schema collection.
/// </summary>
/// <param name="SchemaName">The schema the collection belongs to.</param>
/// <param name="Collection">The definition to create it from.</param>
public sealed record CreateXmlSchemaCollection(
    SqlIdentifier SchemaName,
    XmlSchemaCollection Collection
) : MigrationAction;
