using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Composing a file from documents: several contributors' statements concatenated, introduced by a file header. The
/// header is ordinary comment trivia on the first statement, which is where a parsed file carries it too.
/// </summary>
public sealed class NsqlDocumentTests
{
    private static NsqlDocument Engine() =>
        new([SettingsStatement.Engine().WithSetting("version", "[5.0,6.0)")]);

    private static NsqlDocument State() =>
        new([SettingsStatement.State("file").WithSetting("path", "./nschema.state.json")]);

    [Fact]
    public void Concat_JoinsStatementsInOrder()
    {
        // Act
        var document = NsqlDocument.Concat(Engine(), State());

        // Assert
        document.Statements.OfType<SettingsStatement>().Select(statement => statement.Keyword)
            .ShouldBe([SettingsKeyword.Engine, SettingsKeyword.State]);
    }

    [Fact]
    public void Concat_SeparatesTheStatementsItJoins()
    {
        // Act — the writer owns the separators, so concatenating documents never runs statements together.
        var text = NsqlWriter.Write(NsqlDocument.Concat(Engine(), State()));

        // Assert
        text.ShouldContain(");\n\nSTATE file (");
    }
}
