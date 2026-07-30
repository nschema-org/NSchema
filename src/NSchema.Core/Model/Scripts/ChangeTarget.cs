namespace NSchema.Model.Scripts;

/// <summary>
/// The structural change a change-event script targets.
/// </summary>
public sealed record ChangeTarget(
    SqlIdentifier? Schema,
    SqlIdentifier Table,
    SqlIdentifier Member,
    ChangeTrigger Trigger
)
{
    /// <summary>
    /// The target table, when the target is schema-scoped.
    /// </summary>
    public ObjectAddress? TableAddress => Schema is { } schema ? new ObjectAddress(schema, Table) : null;

    /// <summary>
    /// The fully qualified target path.
    /// </summary>
    public string Path => $"{Schema}.{Table}.{Member}";
}
