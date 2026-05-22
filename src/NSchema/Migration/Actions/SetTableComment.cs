namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the modification of a comment on an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table whose comment will be modified.</param>
/// <param name="TableName">The name of the table whose comment will be modified.</param>
/// <param name="OldComment">The current comment on the table before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the table after modification. This can be null if the comment is being removed.</param>
public sealed record SetTableComment(string SchemaName, string TableName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
