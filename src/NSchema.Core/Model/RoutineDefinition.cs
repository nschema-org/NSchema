namespace NSchema.Model;

/// <summary>
/// The declared spelling of a routine's argument list and definition.
/// </summary>
/// <param name="Address">The routine's address.</param>
/// <param name="Arguments">The declared argument list text.</param>
/// <param name="Definition">The declared definition text.</param>
public sealed record RoutineDefinition(ObjectAddress Address, SqlText Arguments, SqlText Definition);
