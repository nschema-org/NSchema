namespace NSchema.Model;

/// <summary>
/// The declared spelling of a view's body.
/// </summary>
/// <param name="Address">The view's address.</param>
/// <param name="Body">The declared body text.</param>
public sealed record ViewDefinition(ObjectAddress Address, SqlText Body);
