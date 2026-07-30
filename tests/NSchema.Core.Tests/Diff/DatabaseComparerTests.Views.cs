using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_NewView_IsAddCarryingDefinition()
    {
        // Act
        var diff = DiffViews([], [View("active", "SELECT * FROM app.users")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_RemovedView_IsRemove()
    {
        // Act
        var diff = DiffViews([View("active", "SELECT * FROM app.users")], []);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Remove);
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_ViewBodyChange_IsModifyCarryingNewDefinition()
    {
        // Arrange
        var diff = DiffViews(
            [View("active", "SELECT id FROM app.users")],

            // Act
            [View("active", "SELECT id, name FROM app.users")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Definition!.Body.ShouldBe("SELECT id, name FROM app.users"); // replace
        diff.RenamedFrom.ShouldBeNull();
    }

    [Fact]
    public void Compare_ViewCommentOnlyChange_IsModifyWithoutDefinition()
    {
        // Arrange
        var diff = DiffViews(
            [View("active", "SELECT * FROM app.users", comment: "old")],

            // Act
            [View("active", "SELECT * FROM app.users", comment: "new")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Definition.ShouldBeNull(); // no body change -> no replace
        diff.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_RenamedView_SetsRenamedFrom()
    {
        var diff = DiffViews(
            [View("legacy", "SELECT * FROM app.users")],
            [View("active", "SELECT * FROM app.users")],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.View("app", "legacy"), "active")]));

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("legacy");
        diff.Name.ShouldBe("active");
        diff.Definition.ShouldBeNull(); // body unchanged, so it is a rename only
    }

    [Fact]
    public void Compare_UnchangedView_ProducesNoDiff()
        => DiffViews([View("active", "SELECT * FROM app.users")], [View("active", "SELECT * FROM app.users")]).ShouldBeNull();

    // A database stores a view's definition and re-emits it in its own canonical form. Insignificant
    // whitespace and a trailing terminator are cosmetic and must not read as drift, or a clean
    // import → plan would show a phantom change. (Semantic rewrites — qualification, * expansion,
    // injected casts — are reconciled provider-side and are out of scope here.)
    [Fact]
    public void Compare_ViewBodyDifferingOnlyInWhitespace_ProducesNoDiff()
        => DiffViews(
            [View("active", "SELECT id, name\n  FROM app.users\n  WHERE balance > 0")],
            [View("active", "SELECT id, name FROM app.users WHERE balance > 0")]).ShouldBeNull();

    [Fact]
    public void Compare_ViewBodyDifferingOnlyInTrailingSemicolon_ProducesNoDiff()
        => DiffViews(
            [View("active", "SELECT * FROM app.users;")],
            [View("active", "SELECT * FROM app.users")]).ShouldBeNull();

    // Whitespace *inside* a string literal is significant — normalization must not conflate the two,
    // or we would silently miss a real change.
    [Fact]
    public void Compare_ViewBodyWhitespaceInsideStringLiteral_IsSignificant()
        => DiffViews(
            [View("labelled", "SELECT 'a  b' AS label FROM app.users")],
            [View("labelled", "SELECT 'a b' AS label FROM app.users")])!.Change.ShouldBe(ChangeKind.Modify);

}
