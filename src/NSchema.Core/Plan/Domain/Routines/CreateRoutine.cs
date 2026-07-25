using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents the creation (or in-place body replacement) of a routine.
/// </summary>
/// <param name="SchemaName">The name of the schema the routine belongs to.</param>
/// <param name="Routine">The definition of the routine to create or replace.</param>
public sealed record CreateRoutine(SqlIdentifier SchemaName, Routine Routine) : MigrationAction;
