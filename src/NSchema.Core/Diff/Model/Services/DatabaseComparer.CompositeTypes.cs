using NSchema.Diff.Model.CompositeTypes;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Model.Services;

internal sealed partial class DatabaseComparer
{
    private List<CompositeTypeDiff> CompareCompositeTypes(SqlIdentifier schemaName, IReadOnlyList<CompositeType> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.CompositeTypes,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, ObjectKind.CompositeType)),
            type => new CompositeTypeDiff(schemaName, type.Name, ChangeKind.Remove),
            type => BuildNewCompositeType(schemaName, type),
            (currentType, desiredType, renamedFrom) => BuildModifiedCompositeType(schemaName, currentType, desiredType, renamedFrom));

    private static CompositeTypeDiff BuildNewCompositeType(SqlIdentifier schema, CompositeType type) =>
        new(schema, type.Name, ChangeKind.Add, Definition: type, Comment: ValueChange.Between(null, type.Comment));

    // A composite type's every change is applied in place (ALTER TYPE), so there is no recreate: a rename, the
    // comment, and each field add/drop/retype are tracked independently. Fields are matched by name; a type
    // change on a matched field is an in-place retype, not a drop + add.
    private CompositeTypeDiff? BuildModifiedCompositeType(SqlIdentifier schema, CompositeType current, CompositeType desired, SqlIdentifier? renamedFrom)
    {
        var comment = ValueChange.Between(current.Comment, desired.Comment);
        var fields = CompareCompositeFields(current.Fields, desired.Fields);

        if (renamedFrom is null && comment is null && fields.Count == 0)
        {
            return null;
        }

        return new CompositeTypeDiff(schema, desired.Name, ChangeKind.Modify, renamedFrom, null, fields, comment);
    }

    private List<CompositeFieldDiff> CompareCompositeFields(IReadOnlyList<CompositeField> current, IReadOnlyList<CompositeField> desired)
    {
        var result = new List<CompositeFieldDiff>();

        foreach (var currentField in current)
        {
            var match = desired.FirstOrDefault(d => d.Name == currentField.Name);
            if (match is null)
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Remove, currentField.Name));
            }
            else if (!equivalence.Types.Equals(match.DataType, currentField.DataType))
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Modify, currentField.Name, Type: new ValueChange<SqlType>(currentField.DataType, match.DataType)));
            }
        }

        foreach (var desiredField in desired)
        {
            if (current.All(c => c.Name != desiredField.Name))
            {
                result.Add(new CompositeFieldDiff(ChangeKind.Add, desiredField.Name, desiredField));
            }
        }

        return result;
    }
}
