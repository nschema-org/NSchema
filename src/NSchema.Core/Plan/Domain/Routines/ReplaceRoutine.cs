using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Plan.Domain.Routines;

/// <summary>
/// Represents the in-place body replacement of an existing routine.
/// </summary>
/// <param name="SchemaName">The name of the schema the routine belongs to.</param>
/// <param name="Routine">The definition replacing the existing body.</param>
public sealed record ReplaceRoutine(SqlIdentifier SchemaName, Routine Routine) : MigrationAction;
