using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Reading a configuration file: it parses to blocks carrying their labels and settings, and the
/// configuration and project grammars never mix in one file. Binding into the configuration domain is the
/// assembler's job, covered by its own tests.
/// </summary>
public sealed class NsqlConfigurationTests
{
    private static IReadOnlyList<SettingsStatement> Read(string source)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return [.. result.Value.Statements.OfType<SettingsStatement>()];
    }

    [Fact]
    public void ReadConfiguration_UnlabelledStatement_ParsesTypeAndAttributes()
    {
        // Act
        var statement = Read(
            """
            STATE (
              dialect = 'postgres',
              transaction_mode = 'single'
            );
            """).ShouldHaveSingleItem();

        // Assert
        statement.Keyword.ShouldBe(SettingsKeyword.State);
        statement.Label.ShouldBeNull();
        statement.Settings.Select(a => a.Key).ShouldBe(["dialect", "transaction_mode"]);
        statement.Settings[0].Value.ShouldBe("postgres");
    }

    [Fact]
    public void ReadConfiguration_LabelledStatement_ParsesLabel()
    {
        var statement = Read("STATE file ( path = 'state/app.nsstate' );").ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(SettingsKeyword.State);
        statement.Label!.Value.ShouldBe("file");
    }

    [Fact]
    public void ReadConfiguration_KeywordIsCaseInsensitive()
        => Read("Database postgres ( x = 1 );").ShouldHaveSingleItem().Keyword.ShouldBe(SettingsKeyword.Database);

    [Fact]
    public void ReadConfiguration_ParsesAllValueKinds()
    {
        // Act
        var settings = Read(
            """
            DATABASE postgres (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single
            );
            """).Single().Settings;

        // Assert
        settings.Single(a => a.Key == "schema_search_path").Value.ShouldBe("app");
        settings.Single(a => a.Key == "connection_timeout").Value.ShouldBe("1000");
        settings.Single(a => a.Key == "statement_cache").Value.ShouldBe("-1");
        settings.Single(a => a.Key == "prefer_simple").Value.ShouldBe("true");
        settings.Single(a => a.Key == "ssl").Value.ShouldBe("false");
        settings.Single(a => a.Key == "transaction_mode").Value.ShouldBe("single");
    }

    [Fact]
    public void ReadConfiguration_DottedKey_IsPreservedVerbatim()
        => Read("DATABASE postgres ( pool.max = 10 );").Single().Settings.ShouldHaveSingleItem().Key.ShouldBe("pool.max");

    [Fact]
    public void ReadConfiguration_EmptyAttributeList_IsAllowed()
        => Read("STATE ();").ShouldHaveSingleItem().Settings.ShouldBeEmpty();

    [Fact]
    public void ReadConfiguration_MultipleStatements_KeepDeclarationOrder()
    {
        // Act
        var statements = Read(
            """
            STATE file ( path = 'state/app.nsstate' );
            DATABASE postgres ( schema_search_path = 'app' );
            STATE s3 ( bucket = 'state' );
            """);

        // Assert
        statements.Select(s => s.Keyword).ShouldBe([SettingsKeyword.State, SettingsKeyword.Database, SettingsKeyword.State]);
    }

    [Fact]
    public void ReadConfiguration_DuplicateAttribute_IsAnError()
        => NsqlReader.Read("STATE file ( path = 'a', PATH = 'b' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("more than once");

    [Fact]
    public void Read_UnknownStatement_IsASyntaxError()
        => NsqlReader.Read("WORKSPACE staging ( region = 'eu' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("Unknown statement 'WORKSPACE'");

    // -------------------------------------------------------------------------
    // PLUGIN
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfiguration_PluginStatement_Parses()
    {
        // Act
        var statement = Read("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );")

        // Assert
            .ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(SettingsKeyword.Plugin);
        statement.Label!.Value.ShouldBe("pg");
        statement.Settings.Select(a => a.Key).ShouldBe(["source", "version"]);
    }

    [Fact]
    public void ReadConfiguration_PluginStatement_WithoutLabel_IsAnError()
        => NsqlReader.Read("PLUGIN ( source = 'NSchema.Postgres', version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a label");

    // -------------------------------------------------------------------------
    // ENGINE
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadConfiguration_EngineStatement_Parses()
    {
        var statement = Read("ENGINE ( version = '[5.0,6.0)' );").ShouldHaveSingleItem();

        statement.Keyword.ShouldBe(SettingsKeyword.Engine);
        statement.Label.ShouldBeNull();
        statement.Settings.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Fact]
    public void ReadConfiguration_EngineStatement_WithLabel_IsAnError()
        => NsqlReader.Read("ENGINE prod ( version = '5.0.1' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("takes no label");
}
