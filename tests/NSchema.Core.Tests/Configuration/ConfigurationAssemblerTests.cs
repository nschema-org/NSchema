using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Engine;
using NSchema.Configuration.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Tests.Configuration;

/// <summary>
/// The configuration assembly layer: parsed documents resolve into a <see cref="ConfigurationDefinition"/>, with
/// the statement rules (closed attribute sets, single declarations, resolvable labels) validated here — the
/// reader stays pure syntax, and version enforcement stays a level up in the provider.
/// </summary>
public sealed class ConfigurationAssemblerTests
{
    private const string Plugin = "PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );";

    private static NsqlDocument Doc(string source, string? path = null)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return path is null ? result.Value : result.Value with { FilePath = path };
    }

    [Fact]
    public void Assemble_BuildsTheDefinition()
    {
        // Arrange
        var document = Doc(
            $"""
            ENGINE ( version = '[5.0,6.0)' );
            {Plugin}
            DATABASE pg ( host = 'localhost' );
            STATE file ( path = 'state/app.nsstate' );
            """);

        // Act
        var result = ConfigurationAssembler.Assemble([document]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().ShouldBe(new PluginDeclaration("pg", new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("5.0.1") }));
        result.Value.Engine.ShouldBe(new EngineConfiguration { Version = VersionRange.Parse("[5.0,6.0)") });
        result.Value.Database!.Value("host").ShouldBe("localhost");
        result.Value.State!.Label.ShouldBe("file");
    }

    [Fact]
    public void Assemble_ReferenceAcrossDocuments_Resolves()
        => ConfigurationAssembler.Assemble([Doc("DATABASE pg ( host = 'localhost' );"), Doc(Plugin)]).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Assemble_TranslatesAllValueKinds()
    {
        // Arrange
        var document = Doc(
            $"""
            {Plugin}
            DATABASE pg (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single,
              pool.max = 10
            );
            """);

        // Act
        var database = ConfigurationAssembler.Assemble([document]).Value!.Database!;

        // Assert — every value kind flattens to its string form (the binder converts to the target type later).
        database.Value("schema_search_path").ShouldBe("app");
        database.Value("connection_timeout").ShouldBe("1000");
        database.Value("statement_cache").ShouldBe("-1");
        database.Value("prefer_simple").ShouldBe("true");
        database.Value("ssl").ShouldBe("false");
        database.Value("transaction_mode").ShouldBe("single");
        database.Value("pool.max").ShouldBe("10");
    }

    [Fact]
    public void Assemble_AttributeLookup_IsCaseInsensitive()
    {
        // Act
        var document = Doc($"{Plugin} DATABASE pg ( Dialect = 'postgres' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Value!.Database!.Value("dialect").ShouldBe("postgres");
    }

    [Fact]
    public void Assemble_RangeNotation_PassesThroughVerbatim()
    {
        // Act
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = '[5.0,6.0)' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Value!.Plugins.Single().Package.Version.ShouldBe(VersionRange.Parse("[5.0,6.0)"));
    }

    [Fact]
    public void Assemble_BareVersion_MeansExact()
    {
        // Act
        var document = Doc("ENGINE ( version = '5.1.0' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Value!.Engine!.Version.ShouldBe(VersionRange.Parse("[5.1.0]"));
    }

    [Fact]
    public void Assemble_HostVersion_BuildsTheHostRequirement()
    {
        // Arrange
        var document = Doc("ENGINE ( host_version = '[5.0,6.0)' );");

        // Act
        var engine = ConfigurationAssembler.Assemble([document]).Value!.Engine!;

        // Assert
        engine.Version.ShouldBeNull();
        engine.HostVersion.ShouldBe(VersionRange.Parse("[5.0,6.0)"));
    }

    [Fact]
    public void Assemble_VersionAndHostVersion_BuildsBoth()
    {
        // Act
        var document = Doc("ENGINE ( version = '[5.0,6.0)', host_version = '[5.1,6.0)' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Value!.Engine.ShouldBe(
            new EngineConfiguration { Version = VersionRange.Parse("[5.0,6.0)"), HostVersion = VersionRange.Parse("[5.1,6.0)") });
    }

    [Fact]
    public void Assemble_UnknownLabel_IsAnError()
    {
        // Arrange
        var document = Doc("DATABASE pg ( host = 'localhost' );");

        // Act
        var label = document.Statements.OfType<SettingsStatement>().Single().Label!;

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.UnknownPluginLabel("DATABASE", "pg", label.Position));
    }

    [Fact]
    public void Assemble_UnlabelledReference_IsAnError()
    {
        // Act
        var document = Doc("STATE ( path = 'x' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigurationDiagnostics.UnlabelledReference("STATE", document.Statements.Single().Position));
    }

    [Fact]
    public void Assemble_DuplicatePluginLabel_IsAnError()
    {
        // Act
        var document = Doc($"{Plugin} PLUGIN pg ( source = 'NSchema.Sqlite', version = '5.0.1' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.DuplicatePluginLabel("pg", document.Statements[1].Position));
    }

    [Fact]
    public void Assemble_DuplicatePluginSource_IsAnError()
    {
        // Act
        var document = Doc($"{Plugin} PLUGIN pg2 ( source = 'NSchema.Postgres', version = '5.0.2' );");

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(PluginDiagnostics.DuplicatePluginSource("NSchema.Postgres", document.Statements[1].Position));
    }

    [Theory]
    [InlineData("ENGINE ( version = '5.0.1' ); ENGINE ( version = '5.0.2' );", "ENGINE")]
    [InlineData("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' ); DATABASE pg ( a = 1 ); DATABASE pg ( b = 2 );", "DATABASE")]
    public void Assemble_SecondSingletonStatement_IsAnError(string source, string keyword)
    {
        // Act
        var document = Doc(source);

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigurationDiagnostics.DuplicateStatement(keyword, document.Statements[^1].Position));
    }

    [Fact]
    public void Assemble_MissingPluginAttributes_ReportsEach()
    {
        // Act
        // source and version are both required.
        var errors = ConfigurationAssembler.Assemble([Doc("PLUGIN pg ();")]).Errors.ToList();

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldContain(e => e.Message.Contains("Source"));
        errors.ShouldContain(e => e.Message.Contains("Version"));
    }

    [Fact]
    public void Assemble_UnknownPluginAttribute_IsAnError()
        => ConfigurationAssembler.Assemble([Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1', color = 'red' );")])
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Assemble_IntegerVersion_IsAcceptedAsExact()
        // A non-string value stringifies, so an unquoted 5 is the exact version 5.0.0.
        => ConfigurationAssembler.Assemble([Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 5 );")])
            .Value!.Plugins.Single().Package.Version.ShouldBe(VersionRange.Parse("5"));

    [Fact]
    public void Assemble_InvalidVersionRange_IsAnError()
        => ConfigurationAssembler.Assemble([Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 'banana' );")])
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Assemble_InvalidSource_IsAnError()
        => ConfigurationAssembler.Assemble([Doc("PLUGIN pg ( source = 'not a package', version = '5.0.1' );")])
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Assemble_EngineWithoutAssertion_IsVacuous()
        // An ENGINE that asserts neither a version nor a host_version is a harmless no-op, not an error.
        => ConfigurationAssembler.Assemble([Doc("ENGINE ();")]).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Assemble_UnknownEngineAttribute_IsAnError()
        => ConfigurationAssembler.Assemble([Doc("ENGINE ( version = '5.0.1', color = 'red' );")])
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Assemble_InvalidStatement_IsExcludedFromTheDefinition()
    {
        // Arrange
        var document = Doc("PLUGIN pg ( source = 'NSchema.Postgres', version = 'banana' );");

        // Act
        var result = ConfigurationAssembler.Assemble([document]);

        // Assert — the failure still carries the best-effort definition.
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plugins.ShouldBeEmpty();
    }

    [Fact]
    public void Assemble_StampsTheDocumentFile()
    {
        // Act
        var document = Doc("DATABASE pg ( host = 'localhost' );", "config.env.prod.sql");

        // Assert
        ConfigurationAssembler.Assemble([document]).Errors.ShouldHaveSingleItem().File.ShouldBe("config.env.prod.sql");
    }
}
