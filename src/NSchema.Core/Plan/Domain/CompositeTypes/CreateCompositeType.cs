using NSchema.Model;
using NSchema.Model.CompositeTypes;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents the creation of a composite type, with its fields inline.
/// </summary>
/// <param name="SchemaName">The name of the schema the composite type belongs to.</param>
/// <param name="CompositeType">The definition of the composite type to create.</param>
public sealed record CreateCompositeType(SqlIdentifier SchemaName, CompositeType CompositeType) : MigrationAction;
