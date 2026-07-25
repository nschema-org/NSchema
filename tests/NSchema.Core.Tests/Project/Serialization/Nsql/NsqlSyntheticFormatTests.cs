using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Backends;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Tokens;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The formatter printing a synthetic (factory-built) tree: no source, no trivia, yet the output must be
/// syntactically valid and round-trip back to the model it was built from. This is the property Phase 4 rests on.
/// </summary>
public sealed class NsqlSyntheticFormatTests
{
    private readonly DatabaseComparer _comparer = new(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence());

    [Fact]
    public void Format_SyntheticSchema_IsValidAndRoundTrips()
    {
        // Arrange
        var database = new Database { Schemas = [new Schema { Name = "app", Comment = "the app schema" }] };
        var document = SyntaxBuilder.Build(database, declareSchemas: true);

        // Act
        var text = NsqlWriter.Write(document);

        // Assert — parses cleanly and reconstructs the schema, comment and all.
        NsqlReader.Read(text).IsSuccess.ShouldBeTrue();
        var schema = new TestNsqlParser(text).Parse().Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");
        schema.Comment.ShouldBe("the app schema");
    }

    [Fact]
    public void Format_SyntheticRichSchema_RoundTripsWithoutDiff()
    {
        // Arrange — every domain feature, built synthetically.
        var original = TestData.RichSchema();
        var document = SyntaxBuilder.Build(original, declareSchemas: true);

        // Act
        var text = NsqlWriter.Write(document);

        // Assert — reading the printed tree straight back shows no drift.
        var reparsed = new TestNsqlParser(text).Parse().Database;
        _comparer.Compare(AlignedDatabase.Unaligned(original), reparsed).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Format_SyntheticRichDirectives_RoundTrips()
    {
        // Arrange — a directive of every kind (scripts, renames), built synthetically.
        var database = TestData.RichSchema();
        var directives = TestData.RichDirectives();

        // Act
        var text = NsqlWriter.Write(SyntaxBuilder.Build(database, directives, declareSchemas: true));

        // Assert — parses, assembles, and re-emits identically to the original project.
        var read = NsqlReader.Read(text);
        read.IsSuccess.ShouldBeTrue(string.Join("; ", read.Errors.Select(e => e.Message)));
        var assembled = NSchema.Project.ProjectAssembler.Assemble([read.Value]);
        assembled.IsSuccess.ShouldBeTrue(string.Join("; ", assembled.Diagnostics.Select(d => d.Message)));

        var project = assembled.Value!;
        NsqlWriter.Write(project.Database, project.Directives).ShouldBe(NsqlWriter.Write(database, directives));
    }

    [Fact]
    public void Format_SyntheticBlock_IsValidAndRoundTrips()
    {
        // Arrange — a factory-built LOCK statement (the shape the lockfile writer will produce).
        var document = new NsqlDocument([
            new SettingsStatement(SettingsKeyword.Lock, null, new SeparatedSyntaxList<Setting>([
                new Setting("source", "NSchema.Postgres"),
                new Setting("version", "5.0.0-alpha.2"),
            ])),
        ]);

        // Act
        var text = NsqlWriter.Write(document);

        // Assert
        var read = NsqlReader.Read(text);
        read.IsSuccess.ShouldBeTrue(string.Join("; ", read.Errors.Select(e => e.Message)));
        var statement = read.Value.Statements.OfType<SettingsStatement>().ShouldHaveSingleItem();
        statement.Keyword.ShouldBe(SettingsKeyword.Lock);
        statement.Settings.Select(a => (a.Key, a.Value)).ShouldBe([("source", "NSchema.Postgres"), ("version", "5.0.0-alpha.2")]);
    }

    [Fact]
    public void Format_SyntheticTemplate_IsValidAndRoundTrips()
    {
        // Arrange — a factory-built table template and its application.
        var member = new ColumnDefinition(Identifier.Synthetic("note"), new TypeName(null, Identifier.Synthetic("text")));
        var document = new NsqlDocument([
            new TableTemplateStatement(Identifier.Synthetic("audit"), new SeparatedSyntaxList<TableMember>([member]))
            {
                ForKeyword = Token.Keyword("FOR"),
                KindKeyword = Token.Keyword("TABLE"),
            },
            new ApplyTemplateStatement(Identifier.Synthetic("users"), new SeparatedSyntaxList<Identifier>([Identifier.Synthetic("app")])),
        ]);

        // Act
        var text = NsqlWriter.Write(document);

        // Assert — parses cleanly with the template and its application intact.
        var read = NsqlReader.Read(text);
        read.IsSuccess.ShouldBeTrue(string.Join("; ", read.Errors.Select(e => e.Message)));
        read.Value.Statements.OfType<TableTemplateStatement>().ShouldHaveSingleItem().Name.Value.ShouldBe("audit");
        read.Value.Statements.OfType<ApplyTemplateStatement>().ShouldHaveSingleItem().TemplateName.Value.ShouldBe("users");
    }
}
