namespace NSchema.Model;

/// <summary>
/// The declared spelling of a check constraint's expression.
/// </summary>
/// <param name="Address">The constraint's address.</param>
/// <param name="Expression">The declared expression text.</param>
public sealed record CheckConstraintDefinition(MemberAddress Address, SqlText Expression);
