using NSchema.Diff.Domain.Columns;
using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private List<ColumnDiff> CompareColumns(ObjectAddress owner, IReadOnlyList<Column> current, IReadOnlyList<Column> desired, RenameLog renames)
    {
        var result = new List<ColumnDiff>();
        var (forDesired, forCurrent) = NamedEntityMatcher.Match(current, desired);

        for (var j = 0; j < current.Count; j++)
        {
            if (forCurrent[j] is not null)
            {
                LogColumnExists(owner, current[j].Name);
            }
            else
            {
                LogColumnNotInDesired(owner, current[j].Name);
                result.Add(ColumnDiff.Removed(current[j]));
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var desiredCol = desired[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogColumnNew(owner, desiredCol.Name);
                result.Add(ColumnDiff.Added(desiredCol));
            }
            else
            {
                var renamedFrom = renames.RenamedFrom(new MemberAddress(owner.Schema, owner.Name, desiredCol.Name));
                if (BuildModifiedColumn(owner, matchingCurrent, desiredCol, renamedFrom) is { } col)
                {
                    result.Add(col);
                }
            }
        }

        return result;
    }

    private ColumnDiff? BuildModifiedColumn(ObjectAddress owner, Column current, Column desired, SqlIdentifier? renamedFrom)
    {
        if (renamedFrom is null)
        {
            LogColumnUnchanged(owner, desired.Name);
        }
        else
        {
            LogColumnRenamed(owner, renamedFrom, desired.Name);
        }

        ValueChange<SqlType>? type = null;
        if (equivalence.Types.Equals(current.Type, desired.Type))
        {
            LogColumnTypeUnchanged(owner, desired.Name, desired.Type);
        }
        else
        {
            LogColumnTypeChanged(owner, desired.Name, current.Type, desired.Type);
            type = new ValueChange<SqlType>(current.Type, desired.Type);
        }

        ValueChange<bool>? nullability = null;
        if (current.IsNullable == desired.IsNullable)
        {
            LogColumnNullabilityUnchanged(owner, desired.Name, desired.IsNullable ? "NULL" : "NOT NULL");
        }
        else
        {
            LogColumnNullabilityChanged(owner, desired.Name, current.IsNullable, desired.IsNullable);
            nullability = new ValueChange<bool>(current.IsNullable, desired.IsNullable);
        }

        ValueChange<SqlDefaultExpression>? @default = null;
        if (equivalence.Defaults.Equals(current.DefaultExpression, desired.DefaultExpression))
        {
            LogColumnDefaultUnchanged(owner, desired.Name, desired.DefaultExpression?.Value ?? "no default");
        }
        else
        {
            LogColumnDefaultChanged(owner, desired.Name, current.DefaultExpression, desired.DefaultExpression);
            @default = new ValueChange<SqlDefaultExpression>(current.DefaultExpression, desired.DefaultExpression);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogColumnCommentChanged(owner, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        // Storage counts as a change to the generated column, not a separate one.
        ValueChange<SqlText>? generated = null;
        var generatedChanged = current.GeneratedExpression != desired.GeneratedExpression;
        var storageChanged = current.GeneratedExpression is not null
            && desired.GeneratedExpression is not null
            && current.IsStored != desired.IsStored;

        if (generatedChanged || storageChanged)
        {
            generated = new ValueChange<SqlText>(current.GeneratedExpression, desired.GeneratedExpression);
        }

        // Row-guid is its own change: nothing else about the column moves with it, and losing it is invisible in
        // every other field.
        ValueChange<bool>? rowGuid = null;
        if (current.IsRowGuid != desired.IsRowGuid)
        {
            rowGuid = new ValueChange<bool>(current.IsRowGuid, desired.IsRowGuid);
        }

        // Identity changes when the column is toggled into or out of identity, or when both columns are
        // identity but their sequence options differ. Old/New options are null on the side that isn't identity.
        // Each side's options are folded onto the engine's defaults first: a catalog reports a bound whether or not
        // it was declared, so an identity that asked for nothing would otherwise differ from itself forever.
        ValueChange<IdentityOptions>? identity = null;
        var currentIdentity = Fold(current);
        var desiredIdentity = Fold(desired);
        var identityToggled = current.IsIdentity != desired.IsIdentity;
        var identityOptionsChanged = current.IsIdentity && desired.IsIdentity && currentIdentity != desiredIdentity;
        if (identityToggled || identityOptionsChanged)
        {
            var oldOptions = current.IsIdentity ? currentIdentity : null;
            var newOptions = desired.IsIdentity ? desiredIdentity : null;
            LogColumnIdentityChanged(owner, desired.Name,
                oldOptions?.StartWith, newOptions?.StartWith,
                oldOptions?.MinValue, newOptions?.MinValue,
                oldOptions?.IncrementBy, newOptions?.IncrementBy);
            identity = new ValueChange<IdentityOptions>(oldOptions, newOptions);
        }

        if (renamedFrom is null && type is null && nullability is null && @default is null && comment is null && identity is null && generated is null && rowGuid is null)
        {
            return null;
        }

        return ColumnDiff.Modified(desired) with
        {
            RenamedFrom = renamedFrom,
            Type = type,
            Nullability = nullability,
            Default = @default,
            Identity = identity,
            Comment = comment,
            Generated = generated,
            RowGuid = rowGuid,
        };
    }

    // An identity's bounds follow the column's type, which is why the type goes with the options. An identity that
    // asked for nothing is the same schema whether that is recorded as no options at all or as a set of unstated
    // ones — a project writes the first and an introspection that folded every engine default away leaves the
    // second — so both fold to null.
    private IdentityOptions? Fold(Column column)
    {
        if (column.IdentityOptions is not { } options)
        {
            return null;
        }

        var folded = equivalence.WithDefaults(options, column.Type);
        return folded == NothingDeclared ? null : folded;
    }

    private static readonly IdentityOptions NothingDeclared = new(null, null, null);
}
