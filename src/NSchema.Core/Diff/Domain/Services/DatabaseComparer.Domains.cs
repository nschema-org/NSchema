using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private List<DomainDiff> CompareDomains(SqlIdentifier schemaName, IReadOnlyList<DomainType> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Domains,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, SchemaObjectKind.Domain)),
            domain => DomainDiff.Removed(schemaName, domain.Name),
            domain => BuildNewDomain(schemaName, domain),
            (currentDomain, desiredDomain, renamedFrom) => BuildModifiedDomain(schemaName, currentDomain, desiredDomain, renamedFrom));

    private static DomainDiff BuildNewDomain(SqlIdentifier schema, DomainType domain) =>
        DomainDiff.Added(schema, domain);

    // The base type cannot be altered in place (no ALTER DOMAIN … TYPE), so a change to it is a drop + recreate;
    // the default, not-null and checks then ride along on the definition. Every other change (default, not-null,
    // checks, comment, rename) is applied in place, since a domain is depended on by columns and must not be
    // dropped to be modified.
    private DomainDiff? BuildModifiedDomain(SqlIdentifier schema, DomainType current, DomainType desired, SqlIdentifier? renamedFrom)
    {
        var dataType = equivalence.Types.Equals(current.DataType, desired.DataType) ? null : new ValueChange<SqlType>(current.DataType, desired.DataType);
        var comment = ValueChange.Between(current.Comment, desired.Comment);
        var requiresRecreate = dataType is not null;

        // On a recreate the default/not-null/checks are rebuilt from the definition, so they are not diffed in place.
        var @default = requiresRecreate || equivalence.Defaults.Equals(current.Default, desired.Default)
            ? null
            : new ValueChange<SqlDefaultExpression>(current.Default, desired.Default);
        var notNull = requiresRecreate || current.NotNull == desired.NotNull
            ? null
            : new ValueChange<bool>(current.NotNull, desired.NotNull);
        IReadOnlyList<CheckConstraintDiff> checks = requiresRecreate
            ? []
            : CompareTableMembers(new ObjectAddress(schema, desired.Name), "DomainType check", current.Checks, desired.Checks,
                CheckConstraintDiff.Added, CheckConstraintDiff.Removed, CheckConstraintDiff.CommentChanged);

        if (renamedFrom is null && dataType is null && @default is null && notNull is null && checks.Count == 0 && comment is null)
        {
            return null;
        }

        return DomainDiff.Modified(schema, desired.Name) with
        {
            RenamedFrom = renamedFrom,
            Definition = requiresRecreate ? desired : null,
            DataType = dataType,
            Default = @default,
            NotNull = notNull,
            Checks = checks,
            Comment = comment,
        };
    }
}
