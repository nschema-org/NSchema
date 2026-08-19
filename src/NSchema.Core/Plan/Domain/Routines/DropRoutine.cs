using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents the removal of an existing routine.
/// </summary>
/// <param name="Routine">The address of the routine.</param>
/// <param name="Kind">The kind of routine being dropped.</param>
/// <param name="Arguments">The declared argument list.</param>
public sealed record DropRoutine(ObjectAddress Routine, RoutineKind Kind, SqlText? Arguments = null) : MigrationAction;
