using NSchema.Model.Constraints;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Constraints;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders adding a check constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddCheckConstraint(AddCheckConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD {CheckConstraintClause(action.CheckConstraint)}");

    /// <summary>
    /// Renders dropping a check constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropCheckConstraint(DropCheckConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Constraint.Owner)} DROP CONSTRAINT {Quote(action.Constraint.Member)}");

    /// <summary>
    /// Renders adding a unique constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddUniqueConstraint(AddUniqueConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD {UniqueConstraintClause(action.UniqueConstraint)}");

    /// <summary>The <c>CONSTRAINT … UNIQUE (…)</c> clause, used inline in a CREATE TABLE and by the ALTER add.</summary>
    protected string UniqueConstraintClause(UniqueConstraint unique) =>
        $"CONSTRAINT {Quote(unique.Name)} UNIQUE ({ColumnList(unique.ColumnNames)})";

    /// <summary>The <c>CONSTRAINT … CHECK (…)</c> clause, used inline in a CREATE TABLE and by the ALTER add.</summary>
    protected string CheckConstraintClause(CheckConstraint check) =>
        $"CONSTRAINT {Quote(check.Name)} CHECK ({check.Expression})";

    /// <summary>
    /// Renders dropping a unique constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropUniqueConstraint(DropUniqueConstraint action) =>
        Statement($"ALTER TABLE {Qualify(action.Constraint.Owner)} DROP CONSTRAINT {Quote(action.Constraint.Member)}");

    /// <summary>
    /// Renders adding an exclusion constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddExclusionConstraint(AddExclusionConstraint action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping an exclusion constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropExclusionConstraint(DropExclusionConstraint action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a constraint's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetConstraintComment(SetConstraintComment action) =>
        Unsupported(action);
}
