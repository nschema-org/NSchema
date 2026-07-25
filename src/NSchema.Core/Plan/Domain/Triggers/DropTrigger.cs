using NSchema.Model;

namespace NSchema.Plan.Domain.Triggers;

/// <summary>
/// Represents the removal of an existing trigger from a table.
/// </summary>
/// <param name="Trigger">The address of the trigger.</param>
public sealed record DropTrigger(MemberAddress Trigger) : MigrationAction;
