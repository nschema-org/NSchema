namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the modification of the comment associated with an existing schema in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema whose comment will be modified.</param>
/// <param name="OldComment">The current comment on the schema before modification. This can be null if there is no existing comment.</param>
/// <param name="NewComment">The new comment to be set on the schema after modification. This can be null if the comment is being removed.</param>
public sealed record SetSchemaComment(string SchemaName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
