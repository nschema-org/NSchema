namespace NSchema.Model;

/// <summary>
/// The declared spelling of an index's filter predicate.
/// </summary>
/// <param name="Address">The index's address.</param>
/// <param name="Predicate">The declared predicate text, where the index has one.</param>
public sealed record IndexPredicateDefinition(
    MemberAddress Address,
    SqlText? Predicate = null
);
