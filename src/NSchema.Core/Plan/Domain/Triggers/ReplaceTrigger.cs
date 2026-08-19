using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Plan.Domain.Triggers;

/// <summary>
/// Represents the replacement of an existing trigger with a new definition.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Trigger">The definition replacing the existing trigger.</param>
public sealed record ReplaceTrigger(ObjectAddress Table, Trigger Trigger) : MigrationAction;
