using NSchema.Diff.Domain.Routines;
using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private static List<RoutineDiff> CompareRoutines(SqlIdentifier schemaName, IReadOnlyList<Routine> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Routines,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, ObjectKind.Routine)),
            routine => RoutineDiff.Removed(schemaName, routine.Name, routine.RoutineKind),
            routine => BuildNewRoutine(schemaName, routine),
            (currentRoutine, desiredRoutine, renamedFrom) => BuildModifiedRoutine(schemaName, currentRoutine, desiredRoutine, renamedFrom));

    private static RoutineDiff BuildNewRoutine(SqlIdentifier schema, Routine routine) =>
        RoutineDiff.Added(schema, routine);

    // The arguments and definition are opaque, compared for cosmetic equivalence. A definition-only change is
    // a replace (the provider emits CREATE OR REPLACE); an argument change forces a drop + recreate, because a
    // replace under a different signature would create a separate overload rather than replacing — so the diff
    // carries the argument transition and the full desired definition rides along for the recreate.
    private static RoutineDiff? BuildModifiedRoutine(SqlIdentifier schema, Routine current, Routine desired, SqlIdentifier? renamedFrom)
    {
        var argumentsChanged = !current.Arguments.EquivalentTo(desired.Arguments);
        var definitionChanged = !current.Definition.EquivalentTo(desired.Definition);
        var comment = ValueChange.Between(current.Comment, desired.Comment);

        // A kind change (function ⇄ procedure under the same name) also forces a recreate; it surfaces as an
        // argument change so the diff carries the transition and recreates.
        var kindChanged = current.RoutineKind != desired.RoutineKind;

        if (renamedFrom is null && !argumentsChanged && !definitionChanged && comment is null && !kindChanged)
        {
            return null;
        }

        var recreate = argumentsChanged || kindChanged;
        return RoutineDiff.Modified(schema, desired.Name, desired.RoutineKind) with
        {
            RenamedFrom = renamedFrom,
            Definition = recreate || definitionChanged ? desired : null,
            Arguments = recreate ? new ValueChange<SqlText>(current.Arguments, desired.Arguments) : null,
            Comment = comment,
        };
    }
}
