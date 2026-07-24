using NSchema.Model;

namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents the modification of the default value of an existing column in a table within the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="OldDefault">The current default value of the column before modification. This can be null if there is no existing default value.</param>
/// <param name="NewDefault">The new default value to be set on the column after modification. This can be null if the default value is being removed.</param>
public sealed record SetColumnDefault(
    MemberAddress Column,
    SqlDefaultExpression? OldDefault,
    SqlDefaultExpression? NewDefault
) : MigrationAction;
