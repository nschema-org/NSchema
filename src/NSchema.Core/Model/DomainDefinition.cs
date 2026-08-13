namespace NSchema.Model;

/// <summary>
/// The declared spelling of a domain's default expression.
/// </summary>
/// <param name="Address">The domain's address.</param>
/// <param name="Default">The declared default expression, where the domain has one.</param>
public sealed record DomainDefinition(
    ObjectAddress Address,
    SqlDefaultExpression? Default = null
);
