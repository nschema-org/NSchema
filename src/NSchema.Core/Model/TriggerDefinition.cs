namespace NSchema.Model;

/// <summary>
/// The declared spelling of a trigger's opaque SQL fragments.
/// </summary>
/// <param name="Address">The trigger's address.</param>
/// <param name="When">The declared <c>WHEN</c> condition, if any.</param>
/// <param name="FunctionArguments">The declared function arguments, if any.</param>
/// <param name="Body">The declared inline body, if any.</param>
public sealed record TriggerDefinition(MemberAddress Address, SqlText? When, SqlText? FunctionArguments, SqlText? Body);
