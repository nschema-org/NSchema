using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents setting, changing, or clearing the comment on a routine.
/// </summary>
/// <param name="Routine">The address of the routine.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
/// <param name="Kind">Whether the routine is a function or a procedure.</param>
public sealed record SetRoutineComment(ObjectAddress Routine, string? OldComment, string? NewComment, RoutineKind Kind) : MigrationAction;
