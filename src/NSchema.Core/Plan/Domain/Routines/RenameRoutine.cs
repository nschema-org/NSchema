using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents renaming an existing routine.
/// </summary>
/// <param name="Routine">The address of the routine.</param>
/// <param name="NewName">The new name of the routine.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
public sealed record RenameRoutine(ObjectAddress Routine, SqlIdentifier NewName, RoutineKind Kind) : MigrationAction;
