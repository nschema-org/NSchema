using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents renaming an existing routine.
/// </summary>
/// <param name="Routine">The address of the routine.</param>
/// <param name="NewName">The new name of the routine.</param>
/// <param name="Kind">The kind of routine being renamed.</param>
/// <param name="Arguments">The declared argument list.</param>
public sealed record RenameRoutine(ObjectAddress Routine, SqlIdentifier NewName, RoutineKind Kind, SqlText? Arguments = null) : MigrationAction;
