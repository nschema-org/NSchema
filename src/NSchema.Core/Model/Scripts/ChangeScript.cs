using System.Text.Json.Serialization;

namespace NSchema.Model.Scripts;

/// <summary>
/// A script bound to a structural change.
/// </summary>
/// <param name="Name">The name that identifies the script.</param>
/// <param name="Sql">The raw SQL to run.</param>
/// <param name="Target">The structural change the script targets.</param>
[method: JsonConstructor]
public sealed record ChangeScript(
    SqlIdentifier Name,
    SqlText Sql,
    ChangeTarget Target
) : Script(Name, Sql, Target.Schema)
{
    /// <summary>
    /// The fully qualified path of the change target (<c>schema.table.member</c>).
    /// </summary>
    public string Path => Target.Path;

    /// <inheritdoc />
    public override string Description => $"{TriggerText(Target.Trigger)} {Target.Path}";

    /// <summary>
    /// The DDL keyword form of a trigger (e.g. <c>ADD COLUMN</c>), as written in the source.
    /// </summary>
    public static string TriggerText(ChangeTrigger trigger) => trigger switch
    {
        ChangeTrigger.AddColumn => "ADD COLUMN",
        ChangeTrigger.AlterColumnType => "ALTER COLUMN TYPE",
        ChangeTrigger.AddConstraint => "ADD CONSTRAINT",
        _ => "UNKNOWN",
    };
}
