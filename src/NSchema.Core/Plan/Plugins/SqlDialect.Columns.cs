using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders adding a column to a table.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> AddColumn(AddColumn action);

    /// <summary>
    /// Renders dropping a column from a table.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropColumn(DropColumn action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} DROP COLUMN {Quote(action.ColumnName)}");

    /// <summary>
    /// Renders the renaming of a column.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameColumn(RenameColumn action) =>
        Statement($"ALTER TABLE {Qualify(action.Column.Owner)} RENAME COLUMN {Quote(action.Column.Member)} TO {Quote(action.NewName)}");

    /// <summary>
    /// Renders changing a column's type or nullability.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> AlterColumn(AlterColumn action);

    /// <summary>
    /// Renders changing a column's identity sequence options.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> AlterIdentitySequence(AlterIdentitySequence action);

    /// <summary>
    /// Renders setting or dropping a column's default expression.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetColumnDefault(SetColumnDefault action) =>
        Statement(action.NewDefault is null
            ? $"ALTER TABLE {Qualify(action.Column.Owner)} ALTER COLUMN {Quote(action.Column.Member)} DROP DEFAULT"
            : $"ALTER TABLE {Qualify(action.Column.Owner)} ALTER COLUMN {Quote(action.Column.Member)} SET DEFAULT {action.NewDefault}");

    /// <summary>
    /// Renders changing a column's stored generation expression.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> SetColumnGenerated(SetColumnGenerated action);

    /// <summary>
    /// Renders setting or clearing a column's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetColumnComment(SetColumnComment action) =>
        Unsupported(action);
}
