using NSchema.Model;

namespace NSchema.Plan.Domain.XmlSchemaCollections;

/// <summary>
/// Represents dropping an XML schema collection. Every column typed by it must have stopped being so first.
/// </summary>
/// <param name="Collection">The address of the collection.</param>
public sealed record DropXmlSchemaCollection(ObjectAddress Collection) : MigrationAction;
