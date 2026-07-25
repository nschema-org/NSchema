using NSchema.Diff.Domain.Enums;
using NSchema.Model;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private static List<EnumDiff> CompareEnums(SqlIdentifier schemaName, IReadOnlyList<EnumType> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Enums,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, ObjectKind.Enum)),
            enumType => EnumDiff.Removed(schemaName, enumType.Name),
            enumType => BuildNewEnum(schemaName, enumType),
            (currentEnum, desiredEnum, renamedFrom) => BuildModifiedEnum(schemaName, currentEnum, desiredEnum, renamedFrom));

    private static EnumDiff BuildNewEnum(SqlIdentifier schema, EnumType enumType) =>
        EnumDiff.Added(schema, enumType);

    // Enum values are additions-only: a value-compatible change carries the anchored additions, while a removal
    // or reorder carries only the old/new value lists (AddedValues stays empty, so RequiresRecreate is true).
    // The diff still records the latter so drift can display it; planning it is rejected by policy.
    private static EnumDiff? BuildModifiedEnum(SqlIdentifier schema, EnumType current, EnumType desired, SqlIdentifier? renamedFrom)
    {
        var comment = ValueChange.Between(current.Comment, desired.Comment);

        ValueChange<IReadOnlyList<EnumLabel>>? values = null;
        List<EnumValueAddition>? additions = null;
        if (!current.Values.SequenceEqual(desired.Values))
        {
            values = new ValueChange<IReadOnlyList<EnumLabel>>(current.Values, desired.Values);
            additions = ComputeValueAdditions(current.Values, desired.Values);
        }

        if (renamedFrom is null && values is null && comment is null)
        {
            return null;
        }

        return EnumDiff.Modified(schema, desired.Name) with
        {
            RenamedFrom = renamedFrom,
            AddedValues = additions ?? [],
            Values = values,
            Comment = comment,
        };
    }

    /// <summary>
    /// Expresses the change from <paramref name="current"/> to <paramref name="desired"/> as anchored value
    /// additions, or returns <see langword="null"/> when it cannot be (a value was removed or reordered).
    /// Greedy two-pointer subsequence matching is exact here because values are unique within an enum.
    /// </summary>
    private static List<EnumValueAddition>? ComputeValueAdditions(IReadOnlyList<EnumLabel> current, IReadOnlyList<EnumLabel> desired)
    {
        var isNew = new bool[desired.Count];
        var c = 0;
        for (var d = 0; d < desired.Count; d++)
        {
            if (c < current.Count && desired[d] == current[c])
            {
                c++;
            }
            else
            {
                isNew[d] = true;
            }
        }

        if (c != current.Count)
        {
            return null;
        }

        // Additions execute in list order, so an After anchor always exists when it runs: it is either a
        // pre-existing value or was added by the previous addition. A run of new values at the head anchors
        // Before the first pre-existing value instead.
        var additions = new List<EnumValueAddition>();
        for (var d = 0; d < desired.Count; d++)
        {
            if (!isNew[d])
            {
                continue;
            }

            additions.Add(d > 0
                ? new EnumValueAddition(desired[d], After: desired[d - 1])
                : current.Count > 0
                    ? new EnumValueAddition(desired[d], Before: current[0])
                    : new EnumValueAddition(desired[d]));
        }
        return additions;
    }
}
