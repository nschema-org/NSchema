namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the modification of a comment on an existing index in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table in which the index's comment will be modified.</param>
/// <param name="TableName">The name of the table in which the index's comment will be modified.</param>
/// <param name="IndexName">The name of the index whose comment will be modified.</param>
/// <param name="OldComment">The current comment on the index before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the index after modification. This can be null if the comment is being removed.</param>
public sealed record SetIndexComment(string SchemaName, string TableName, string IndexName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
