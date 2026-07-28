using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Schemas;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a schema.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateSchema(CreateSchema action) =>
        Statement($"CREATE SCHEMA {Quote(action.SchemaName)}");

    /// <summary>
    /// Renders the removal of a schema.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropSchema(DropSchema action) =>
        Statement($"DROP SCHEMA {Quote(action.SchemaName)}");

    /// <summary>
    /// Renders the renaming of a schema.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameSchema(RenameSchema action) =>
        Statement($"ALTER SCHEMA {Quote(action.OldName)} RENAME TO {Quote(action.NewName)}");

    /// <summary>
    /// Renders granting schema usage to a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> GrantSchemaUsage(GrantSchemaUsage action) =>
        Unsupported(action);

    /// <summary>
    /// Renders revoking schema usage from a role.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RevokeSchemaUsage(RevokeSchemaUsage action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a schema's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetSchemaComment(SetSchemaComment action) =>
        Unsupported(action);
}
