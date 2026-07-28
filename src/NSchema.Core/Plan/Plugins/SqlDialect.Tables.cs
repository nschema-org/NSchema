using System.Text;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Tables;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a table.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateTable(CreateTable action);

    /// <summary>
    /// Renders the removal of a table.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropTable(DropTable action) =>
        Statement($"DROP TABLE {Qualify(action.Table)}");

    /// <summary>
    /// Renders the renaming of a table.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameTable(RenameTable action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} RENAME TO {Quote(action.NewName)}");

    /// <summary>
    /// Renders adding a primary key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddPrimaryKey(AddPrimaryKey action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD {PrimaryKeyClause(action.PrimaryKey)}");

    /// <summary>
    /// Renders dropping a primary key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropPrimaryKey(DropPrimaryKey action) =>
        Statement($"ALTER TABLE {Qualify(action.PrimaryKey.Owner)} DROP CONSTRAINT {Quote(action.PrimaryKey.Member)}");

    /// <summary>
    /// Renders adding a foreign key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddForeignKey(AddForeignKey action) =>
        Statement($"ALTER TABLE {Qualify(action.Table)} ADD {ForeignKeyClause(action.ForeignKey)}");

    /// <summary>
    /// Renders dropping a foreign key constraint.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropForeignKey(DropForeignKey action) =>
        Statement($"ALTER TABLE {Qualify(action.ForeignKey.Owner)} DROP CONSTRAINT {Quote(action.ForeignKey.Member)}");

    /// <summary>
    /// The inline table-constraint clauses for a CREATE TABLE body, in a safe order: primary key, unique
    /// constraints, check constraints, then foreign keys. A dialect appends these to its column definitions.
    /// Exclusion constraints are dialect-specific and left to the dialect that supports them.
    /// </summary>
    protected IEnumerable<string> InlineConstraintClauses(Table table)
    {
        if (table.PrimaryKey is { } pk)
        {
            yield return PrimaryKeyClause(pk);
        }

        foreach (var unique in table.UniqueConstraints)
        {
            yield return UniqueConstraintClause(unique);
        }

        foreach (var check in table.CheckConstraints)
        {
            yield return CheckConstraintClause(check);
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            yield return ForeignKeyClause(foreignKey);
        }
    }

    /// <summary>The <c>CONSTRAINT … PRIMARY KEY (…)</c> clause, used inline in a CREATE TABLE and by the ALTER add.</summary>
    protected string PrimaryKeyClause(PrimaryKey primaryKey) =>
        $"CONSTRAINT {Quote(primaryKey.Name)} PRIMARY KEY ({ColumnList(primaryKey.ColumnNames)})";

    /// <summary>The <c>CONSTRAINT … FOREIGN KEY (…) REFERENCES …</c> clause, used inline in a CREATE TABLE and by the ALTER add.</summary>
    protected string ForeignKeyClause(ForeignKey key)
    {
        var sql = new StringBuilder(
            $"CONSTRAINT {Quote(key.Name)} FOREIGN KEY ({ColumnList(key.ColumnNames)}) " +
            $"REFERENCES {ForeignKeyTarget(key)} ({ColumnList(key.ReferencedColumnNames)})");

        if (key.OnDelete != ReferentialAction.NoAction)
        {
            sql.Append($" ON DELETE {ReferentialActionSql(key.OnDelete)}");
        }

        if (key.OnUpdate != ReferentialAction.NoAction)
        {
            sql.Append($" ON UPDATE {ReferentialActionSql(key.OnUpdate)}");
        }

        return sql.ToString();
    }

    /// <summary>
    /// The referenced-table text in a foreign key's REFERENCES clause. Schema-qualified by default; a dialect
    /// whose foreign keys stay within one database (e.g. Sqlite) overrides this to emit the bare name.
    /// </summary>
    protected virtual string ForeignKeyTarget(ForeignKey key) => Qualify(key.References);

    /// <summary>
    /// Renders granting table privileges to a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> GrantTablePrivileges(GrantTablePrivileges action) =>
        action.Privileges == TablePrivilege.None
            ? Statements()
            : Statement($"GRANT {PrivilegeList(action.Privileges)} ON {Qualify(action.Table)} TO {Quote(action.Role)}");

    /// <summary>
    /// Renders revoking table privileges from a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RevokeTablePrivileges(RevokeTablePrivileges action) =>
        action.Privileges == TablePrivilege.None
            ? Statements()
            : Statement($"REVOKE {PrivilegeList(action.Privileges)} ON {Qualify(action.Table)} FROM {Quote(action.Role)}");

    /// <summary>
    /// Renders setting or clearing a table's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetTableComment(SetTableComment action) =>
        Unsupported(action);

    private static string ReferentialActionSql(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };

    private static string PrivilegeList(TablePrivilege privileges) => string.Join(", ", privileges.SqlNames());
}
