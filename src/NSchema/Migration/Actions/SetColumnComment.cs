namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the modification of a comment on an existing column in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table in which the column's comment will be modified.</param>
/// <param name="TableName">The name of the table in which the column's comment will be modified.</param>
/// <param name="ColumnName">The name of the column whose comment will be modified.</param>
/// <param name="OldComment">The current comment on the column before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the column after modification. This can be null if the comment is being removed.</param>
public sealed record SetColumnComment(string SchemaName, string TableName, string ColumnName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
