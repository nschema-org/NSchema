using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model.Columns;
using NSchema.Plan.Domain;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public sealed class CommentStoragePolicyTests
{
    /// <summary>A plan documenting a table, one of its columns, and the schema they sit in.</summary>
    private static MigrationPlan Documented() => new(
        new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Comment = ValueChange.Between(null, "the application"),
                Tables =
                [
                    TableDiff.Modified("app", "users") with
                    {
                        Comment = ValueChange.Between(null, "everyone"),
                        Columns =
                        [
                            ColumnDiff.Added(new Column { Name = "name", Type = SqlType.Text }) with
                            {
                                Comment = ValueChange.Between(null, "what to call them"),
                            },
                        ],
                    },
                ],
            },
        ]),
        []);

    [Fact]
    public void Validate_NoDialect_PassesClean()
        => new CommentStoragePolicy().Validate(Documented()).ShouldBeEmpty();

    [Fact]
    public void Validate_EngineRecordingComments_PassesClean()
        => new CommentStoragePolicy(new StubSqlDialect()).Validate(Documented()).ShouldBeEmpty();

    [Fact]
    public void Validate_EngineRecordingNoComments_ReportsEverySite()
    {
        // Act
        var diagnostic = new CommentStoragePolicy(new CommentlessDialect()).Validate(Documented()).ShouldHaveSingleItem();

        // Assert — one finding naming every site, rather than one per action skipped while rendering.
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Source.ShouldBe("comments");
        diagnostic.Message.ShouldContain("schema 'app'");
        diagnostic.Message.ShouldContain("'app.users'");
        diagnostic.Message.ShouldContain("'app.users.name'");
    }

    [Fact]
    public void Validate_EngineRecordingNoComments_UndocumentedPlan_PassesClean()
    {
        // Arrange — the capability is not itself a finding; only documentation the engine cannot hold is.
        var plan = new MigrationPlan(new DatabaseDiff([SchemaDiff.Containing("app")]), []);

        // Assert
        new CommentStoragePolicy(new CommentlessDialect()).Validate(plan).ShouldBeEmpty();
    }

    private sealed class CommentlessDialect : StubSqlDialect
    {
        public override bool SupportsComments => false;
    }
}
