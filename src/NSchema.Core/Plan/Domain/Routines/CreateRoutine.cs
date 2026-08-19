using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents the creation of a routine that does not yet exist.
/// </summary>
/// <param name="SchemaName">The name of the schema the routine belongs to.</param>
/// <param name="Routine">The definition of the routine to create.</param>
public sealed record CreateRoutine(SqlIdentifier SchemaName, Routine Routine) : MigrationAction;
