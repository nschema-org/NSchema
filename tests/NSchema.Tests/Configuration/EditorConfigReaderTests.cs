using NSchema.Configuration;

namespace NSchema.Tests.Configuration;

public sealed class EditorConfigReaderTests : IDisposable
{
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-editorconfig-").FullName;

    public void Dispose() => Directory.Delete(_projectDirectory, recursive: true);

    [Fact]
    public void Read_NoEditorConfig_HasNoOverrides()
    {
        // Arrange
        WriteSchema("schema.sql");

        // Act
        var overrides = Read();

        // Assert
        overrides.ByCode.ShouldBeEmpty();
        overrides.BySource.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NoSchemaFiles_HasNoOverrides()
    {
        // Arrange — a project that is all configuration and no schema has nothing to resolve a section against.
        WriteEditorConfig("[*.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var overrides = Read();

        // Assert
        overrides.ByCode.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("none", PolicyEnforcement.Ignore)]
    [InlineData("silent", PolicyEnforcement.Ignore)]
    [InlineData("suggestion", PolicyEnforcement.Allow)]
    [InlineData("info", PolicyEnforcement.Allow)]
    [InlineData("warning", PolicyEnforcement.Warn)]
    [InlineData("error", PolicyEnforcement.Error)]
    public void Read_Severity_MapsToEnforcement(string severity, PolicyEnforcement expected)
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", $"nschema_diagnostic.destructive-change.severity = {severity}");

        // Act
        var overrides = Read();

        // Assert
        overrides.ByCode[new DiagnosticCode("destructive-change")].ShouldBe(expected);
    }

    [Fact]
    public void Read_Severity_IsCaseInsensitive()
    {
        // Arrange — the format lowercases keys but leaves values as the user wrote them.
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", "nschema_diagnostic.destructive-change.severity = None");

        // Act
        var overrides = Read();

        // Assert
        overrides.ByCode[new DiagnosticCode("destructive-change")].ShouldBe(PolicyEnforcement.Ignore);
    }

    [Fact]
    public void Read_Default_LeavesTheFindingAsItsProducerReportedIt()
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", "nschema_diagnostic.destructive-change.severity = default");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert — recognised, so no complaint, but nothing is overridden either.
        result.Diagnostics.ShouldBeEmpty();
        result.Require().ByCode.ShouldBeEmpty();
    }

    [Fact]
    public void Read_SourceKey_OverridesEveryFindingFromThatProducer()
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", "nschema_diagnostic_source.data-hazards.severity = error");

        // Act
        var overrides = Read();

        // Assert
        overrides.BySource[new DiagnosticSource("data-hazards")].ShouldBe(PolicyEnforcement.Error);
        overrides.ByCode.ShouldBeEmpty();
    }

    [Fact]
    public void Read_UnknownSeverity_Fails()
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", "nschema_diagnostic.destructive-change.severity = quiet");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(new DiagnosticCode("unknown-severity"));
        result.Require().ByCode.ShouldBeEmpty();
    }

    [Fact]
    public void Read_UnusableName_Fails()
    {
        // Arrange — a code is hyphen-separated lowercase, so an underscored one is not a code at all.
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]", "nschema_diagnostic.destructive_change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(new DiagnosticCode("invalid-diagnostic-name"));
        result.Require().ByCode.ShouldBeEmpty();
    }

    [Fact]
    public void Read_SectionNarrowerThanTheSchema_Fails()
    {
        // Arrange — enforcement is applied to the run, so a severity only some schema files resolve is refused
        // rather than quietly applied to all of them.
        WriteSchema("schema.sql");
        WriteSchema(Path.Combine("legacy", "old.sql"));
        WriteEditorConfig("[legacy/*.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(new DiagnosticCode("scoped-severity"));
        result.Require().ByCode.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NarrowerSectionAgreeingWithTheWiderOne_IsApplied()
    {
        // Arrange — the same value everywhere is unambiguous however many sections set it.
        WriteSchema("schema.sql");
        WriteSchema(Path.Combine("legacy", "old.sql"));
        WriteEditorConfig(
            "[*.sql]", "nschema_diagnostic.destructive-change.severity = none",
            "[legacy/*.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.Require().ByCode[new DiagnosticCode("destructive-change")].ShouldBe(PolicyEnforcement.Ignore);
    }

    [Fact]
    public void Read_NarrowerSectionOverridingTheWiderOne_Fails()
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteSchema(Path.Combine("legacy", "old.sql"));
        WriteEditorConfig(
            "[*.sql]", "nschema_diagnostic.destructive-change.severity = error",
            "[legacy/*.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(new DiagnosticCode("scoped-severity"));
    }

    [Fact]
    public void Read_UnselectedEnvironmentOverlay_DoesNotConstrainResolution()
    {
        // Arrange — the overlay is not part of the desired schema unless its environment is selected, so a section
        // that misses it is not thereby narrower than the schema.
        WriteSchema("schema.sql");
        WriteSchema("schema.env.prod.sql");
        WriteEditorConfig("[schema.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.Require().ByCode[new DiagnosticCode("destructive-change")].ShouldBe(PolicyEnforcement.Ignore);
    }

    [Fact]
    public void Read_SelectedEnvironmentOverlay_ConstrainsResolution()
    {
        // Arrange — the same project, with the overlay selected and so part of the schema the severity must cover.
        WriteSchema("schema.sql");
        WriteSchema("schema.env.prod.sql");
        WriteEditorConfig("[schema.sql]", "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, "prod");

        // Assert
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(new DiagnosticCode("scoped-severity"));
    }

    [Fact]
    public void Read_KeysForOtherTools_AreIgnored()
    {
        // Arrange
        WriteSchema("schema.sql");
        WriteEditorConfig("[*.sql]",
            "indent_style = space",
            "dotnet_diagnostic.CA1822.severity = none",
            "nschema_diagnostic.destructive-change.severity = none");

        // Act
        var result = EditorConfigReader.Read(_projectDirectory, environment: null);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.Require().ByCode.ShouldHaveSingleItem().Key.ShouldBe(new DiagnosticCode("destructive-change"));
    }

    private DiagnosticOverrides Read() => EditorConfigReader.Read(_projectDirectory, environment: null).Require();

    private void WriteSchema(string relativePath)
    {
        var path = Path.Combine(_projectDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "SCHEMA app;");
    }

    private void WriteEditorConfig(params string[] lines) =>
        File.WriteAllLines(Path.Combine(_projectDirectory, ".editorconfig"), ["root = true", "", .. lines]);
}
