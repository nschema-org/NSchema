namespace NSchema.Model.Scripts;

/// <summary>
/// What identifies a script: the schema its run is scoped to, if any, and its name.
/// </summary>
/// <param name="Schema">The schema the run is scoped to, or <see langword="null"/> when the script is database-wide.</param>
/// <param name="Name">The name that identifies the script.</param>
public sealed record ScriptReference(SqlIdentifier? Schema, SqlIdentifier Name)
{
    /// <inheritdoc />
    public override string ToString() => Schema != null ? $"{Schema}.{Name}" : Name.Value;
}
