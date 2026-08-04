using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents dropping and recreating a routine whose signature changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the routine.</param>
/// <param name="Routine">The desired routine to recreate.</param>
/// <param name="PreviousArguments">The argument list the drop half addresses.</param>
public sealed record RecreateRoutine(SqlIdentifier SchemaName, Routine Routine, SqlText? PreviousArguments = null) : MigrationAction;
