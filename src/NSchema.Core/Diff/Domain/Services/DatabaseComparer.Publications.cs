using NSchema.Diff.Domain.Publications;
using NSchema.Model;
using NSchema.Model.Publications;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    /// <summary>
    /// Compares the publications. Like extensions, the current side only ever holds managed publications, so one
    /// created by another tool never enters the compare.
    /// </summary>
    private static List<PublicationDiff> ComparePublications(
        IReadOnlyList<Publication> current,
        IReadOnlyList<Publication> desired,
        RenameLog renames
    ) =>
        CompareObjects(current, desired,
            name => renames.RenamedFrom(DatabaseAddress.Publication(name)),
            publication => PublicationDiff.Removed(publication.Name),
            PublicationDiff.Added,
            BuildModifiedPublication);

    private static PublicationDiff? BuildModifiedPublication(Publication current, Publication desired, SqlIdentifier? renamedFrom)
    {
        if (current.AllTables != desired.AllTables)
        {
            return PublicationDiff.Recreated(desired, current.AllTables) with { RenamedFrom = renamedFrom };
        }

        var tables = ComparePublishedTables(current.Tables, desired.Tables);
        var schemas = current.Schemas.Except(desired.Schemas).Select(s => new PublishedSchemaChange(ChangeKind.Remove, s))
            .Concat(desired.Schemas.Except(current.Schemas).Select(s => new PublishedSchemaChange(ChangeKind.Add, s)))
            .OrderBy(s => s.Schema)
            .ToList();
        var operations = current.Operations == desired.Operations
            ? null
            : new ValueChange<PublishedOperations>(current.Operations, desired.Operations);
        var comment = ValueChange.Between(current.Comment, desired.Comment);

        if (renamedFrom is null && tables.Count == 0 && schemas.Count == 0 && operations is null && comment is null)
        {
            return null;
        }

        return PublicationDiff.Modified(desired.Name) with
        {
            RenamedFrom = renamedFrom,
            Tables = tables,
            Schemas = schemas,
            Operations = operations,
            Comment = comment,
        };
    }

    private static List<PublishedTableDiff> ComparePublishedTables(IReadOnlyList<PublishedTable> current, IReadOnlyList<PublishedTable> desired)
    {
        var currentByTable = current.ToDictionary(t => Unkinded(t.Table));
        var desiredByTable = desired.ToDictionary(t => Unkinded(t.Table));
        var result = new List<PublishedTableDiff>();

        foreach (var (table, entry) in currentByTable)
        {
            if (!desiredByTable.ContainsKey(table))
            {
                result.Add(PublishedTableDiff.Removed(entry));
            }
        }

        foreach (var (table, entry) in desiredByTable)
        {
            if (!currentByTable.TryGetValue(table, out var previous))
            {
                result.Add(PublishedTableDiff.Added(entry));
            }
            else if (!SameEntry(previous, entry))
            {
                result.Add(PublishedTableDiff.Modified(previous, entry));
            }
        }

        return [.. result.OrderBy(t => t.Table.Schema).ThenBy(t => t.Table.Name)];
    }

    // The entry's address is a reference, so a kind on one side and not the other is not a change.
    private static ObjectAddress Unkinded(ObjectAddress address) => new(address.Schema, address.Name);

    // The filter is opaque, compared for cosmetic equivalence; the recorded spelling handles the rest.
    private static bool SameEntry(PublishedTable current, PublishedTable desired) =>
        current.SameColumns(desired)
        && (current.Filter is null
            ? desired.Filter is null
            : desired.Filter is not null && current.Filter.EquivalentTo(desired.Filter));
}
