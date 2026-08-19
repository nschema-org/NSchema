using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Plan.Domain.Triggers;

/// <summary>
/// Represents adding a new trigger to an existing table.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Trigger">The definition of the trigger to add.</param>
public sealed record CreateTrigger(ObjectAddress Table, Trigger Trigger) : MigrationAction;
