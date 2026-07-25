using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The lockfile grammar: <c>nschema.lock</c> parses to typed <see cref="SettingsStatement"/>s, and neither the
/// configuration nor the project grammar mixes into it. Translation into the plugin domain is the CLI's.
/// </summary>
public sealed class NsqlLockTests
{
    private static IReadOnlyList<SettingsStatement> Read(string source)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return [.. result.Value.Statements.OfType<SettingsStatement>()];
    }

    [Fact]
    public void ReadLock_Statement_ParsesSourceAndVersion()
    {
        // Act
        var statement = Read("LOCK ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );")

        // Assert
            .ShouldHaveSingleItem();

        statement.Settings.Select(a => a.Key).ShouldBe(["source", "version"]);
        statement.Settings[0].Value.ShouldBe("NSchema.Postgres");
        statement.Settings[1].Value.ShouldBe("5.0.0-alpha.2");
    }

    [Fact]
    public void ReadLock_MultipleStatements_KeepDeclarationOrder()
    {
        // Arrange
        var statements = Read(
            """
            LOCK ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );
            LOCK ( source = 'NSchema.Aws',      version = '5.0.0-alpha.2' );
            """);

        // Act
        statements.Select(s => s.Settings[0].Value)

        // Assert
            .ShouldBe(["NSchema.Postgres", "NSchema.Aws"]);
    }

    [Fact]
    public void ReadLock_KeywordIsCaseInsensitive()
        => Read("lock ( source = 'NSchema.Sqlite', version = '5.0.0-alpha.2' );").ShouldHaveSingleItem();

    [Fact]
    public void ReadLock_WithLabel_IsAnError()
        => NsqlReader.Read("LOCK pg ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("takes no label");

    [Fact]
    public void ReadLock_DuplicateAttribute_IsAnError()
        => NsqlReader.Read("LOCK ( source = 'a', SOURCE = 'b' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("more than once");
}
