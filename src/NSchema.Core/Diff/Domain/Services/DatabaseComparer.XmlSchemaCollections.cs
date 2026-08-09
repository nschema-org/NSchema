using NSchema.Diff.Domain.XmlSchemaCollections;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.XmlSchemaCollections;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private static List<XmlSchemaCollectionDiff> CompareXmlSchemaCollections(
        SqlIdentifier schemaName, IReadOnlyList<XmlSchemaCollection> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.XmlSchemaCollections,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, SchemaObjectKind.XmlSchemaCollection)),
            collection => XmlSchemaCollectionDiff.Removed(schemaName, collection.Name),
            collection => XmlSchemaCollectionDiff.Added(schemaName, collection),
            (currentCollection, desiredCollection, renamedFrom) =>
                BuildModifiedXmlSchemaCollection(schemaName, currentCollection, desiredCollection, renamedFrom));

    // The body is opaque, and an engine can only add to a collection, never take away — so a change to what it
    // holds is a rebuild, not an alteration, and the definition rides the diff to recreate it from.
    private static XmlSchemaCollectionDiff? BuildModifiedXmlSchemaCollection(
        SqlIdentifier schema, XmlSchemaCollection current, XmlSchemaCollection desired, SqlIdentifier? renamedFrom)
    {
        var bodyChanged = !current.Body.EquivalentTo(desired.Body);
        var comment = ValueChange.Between(current.Comment, desired.Comment);

        if (renamedFrom is null && !bodyChanged && comment is null)
        {
            return null;
        }

        return XmlSchemaCollectionDiff.Modified(schema, desired.Name) with
        {
            RenamedFrom = renamedFrom,
            Definition = bodyChanged ? desired : null,
            RequiresRecreate = bodyChanged,
            Comment = comment,
        };
    }
}
