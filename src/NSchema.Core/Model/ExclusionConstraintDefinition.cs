namespace NSchema.Model;

/// <summary>
/// The declared spelling of an exclusion constraint's predicate.
/// </summary>
/// <param name="Address">The constraint's address.</param>
/// <param name="Predicate">The declared predicate text, where the constraint has one.</param>
public sealed record ExclusionConstraintDefinition(
    MemberAddress Address,
    SqlText? Predicate = null
);
