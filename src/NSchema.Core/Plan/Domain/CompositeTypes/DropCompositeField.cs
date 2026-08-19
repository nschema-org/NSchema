using NSchema.Model;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents dropping a field from a composite type (<c>ALTER TYPE … DROP ATTRIBUTE …</c>).
/// </summary>
/// <param name="Field">The address of the field.</param>
public sealed record DropCompositeField(MemberAddress Field) : MigrationAction;
