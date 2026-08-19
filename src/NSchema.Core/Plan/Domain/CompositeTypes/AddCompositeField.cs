using NSchema.Model;
using NSchema.Model.CompositeTypes;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents adding a field to a composite type (<c>ALTER TYPE … ADD ATTRIBUTE …</c>).
/// </summary>
/// <param name="Type">The address of the composite type.</param>
/// <param name="Field">The field to add.</param>
public sealed record AddCompositeField(ObjectAddress Type, CompositeField Field) : MigrationAction;
