using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Routines;
using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Routines (functions and procedures — one type with a kind discriminator)
    // -------------------------------------------------------------------------

    private const string Def = "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$";
    private const string ProcDef = "LANGUAGE sql AS $$ DELETE FROM app.t; $$";

    private static Routine Fn(string name, string args, string def, string? comment = null) =>
        new Routine { Name = name, RoutineKind = RoutineKind.Function, Arguments = args, Definition = def, Comment = comment };

    private static Routine Proc(string name, string args, string def, string? comment = null) =>
        new Routine { Name = name, RoutineKind = RoutineKind.Procedure, Arguments = args, Definition = def, Comment = comment };

    /// <summary>Diffs two <c>app</c> schemas holding the given routines, returning the single routine diff (null when unchanged).</summary>
    private RoutineDiff? DiffRoutines(IReadOnlyList<Routine> current, IReadOnlyList<Routine> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Routines = [.. current] }), Db(new Schema { Name = "app", Routines = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.Routines.SingleOrDefault();

    [Fact]
    public void Compare_NewFunction_IsAddCarryingDefinition()
    {
        // Act
        var diff = DiffRoutines([], [Fn("f", "a int", Def)]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.RoutineKind.ShouldBe(RoutineKind.Function);
        diff.Definition!.Arguments.ShouldBe("a int");
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_NewProcedure_CarriesProcedureKind()
        => DiffRoutines([], [Proc("p", "before date", ProcDef)])!.RoutineKind.ShouldBe(RoutineKind.Procedure);

    [Fact]
    public void Compare_RemovedRoutine_IsRemoveCarryingKind()
    {
        // Act
        var diff = DiffRoutines([Proc("p", "", ProcDef)], []);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Remove);
        diff.RoutineKind.ShouldBe(RoutineKind.Procedure);
    }

    [Fact]
    public void Compare_UnchangedRoutine_ProducesNoDiff()
        => DiffRoutines([Fn("f", "a int", Def)], [Fn("f", "a int", Def)]).ShouldBeNull();

    [Fact]
    public void Compare_BodyChange_IsReplaceNotRecreate()
    {
        // Arrange
        var diff = DiffRoutines(
            [Fn("f", "a int", "RETURNS int AS $$ SELECT 1 $$")],

            // Act
            [Fn("f", "a int", "RETURNS int AS $$ SELECT 2 $$")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Definition.ShouldNotBeNull();
        diff.Arguments.ShouldBeNull();
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_ArgumentsChange_RequiresRecreate()
    {
        // Act
        var diff = DiffRoutines([Fn("f", "a int", Def)], [Fn("f", "a int, b text", Def)]);

        // Assert
        diff!.Arguments.ShouldBe(new ValueChange<SqlText>("a int", "a int, b text"));
        diff.Definition.ShouldNotBeNull(); // the desired definition rides along for the recreate
        diff.RequiresRecreate.ShouldBeTrue();
    }

    [Fact]
    public void Compare_CosmeticWhitespace_ProducesNoDiff()
        // Both the argument list and the definition are compared for cosmetic equivalence.
        => DiffRoutines(
            [Fn("f", "a  int,  b text", "RETURNS int\n  LANGUAGE sql AS $$ SELECT 1; $$;")],
            [Fn("f", "a int, b text", "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$")]).ShouldBeNull();

    [Fact]
    public void Compare_Renamed_SetsRenamedFrom()
    {
        var diff = DiffRoutines([Fn("old_f", "", Def)], [Fn("f", "", Def)],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(App("old_f") with { Kind = SchemaObjectKind.Routine }, "f")]));

        diff!.RenamedFrom.ShouldBe("old_f");
        diff.Definition.ShouldBeNull(); // nothing else changed, so it is a rename only
        diff.RequiresRecreate.ShouldBeFalse();
    }

    [Fact]
    public void Compare_CommentOnlyChange_IsModifyWithoutDefinition()
    {
        // Act
        var diff = DiffRoutines([Proc("p", "", ProcDef, comment: "old")], [Proc("p", "", ProcDef, comment: "new")]);

        // Assert
        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_FunctionSwappedToProcedureSameName_IsRecreate()
    {
        // Act
        // Functions and procedures share one name space, so a same-name swap matches by name and is a kind
        // change — applied as a drop + recreate (there is no in-place conversion).
        var diff = DiffRoutines([Fn("r", "", Def)], [Proc("r", "", ProcDef)]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RoutineKind.ShouldBe(RoutineKind.Procedure);
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull();
    }
}
