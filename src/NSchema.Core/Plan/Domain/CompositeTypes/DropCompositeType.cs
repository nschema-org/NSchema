using NSchema.Model;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents the removal of an existing composite type.
/// </summary>
/// <param name="Type">The address of the composite type.</param>
public sealed record DropCompositeType(ObjectAddress Type) : MigrationAction;
