using NSchema.Project.Nsql;

namespace NSchema.Tests.Diagnostics;

public sealed class DiagnosticCollectionTests
{
    private readonly DiagnosticCollection _sut = new();

    private static Diagnostic Info(string message = "fyi") => Diagnostic.Info("source", "info", message);

    private static Diagnostic Error(string message = "boom") => Diagnostic.Error("source", "error", message);

    private static Diagnostic Warning(string message = "careful") => Diagnostic.Warning("source", "warning", message);

    [Fact]
    public void Add_CollectsFindings_InInsertionOrder()
    {
        // Arrange
        var warning = Warning();
        var error = Error();

        // Act
        _sut.Add(warning);
        _sut.AddRange([error]);

        // Assert
        _sut.ShouldBe([warning, error]);
    }

    [Fact]
    public void Constructor_CopiesTheSeed()
    {
        // Arrange
        var seed = new List<Diagnostic> { Warning() };

        // Act
        var collection = new DiagnosticCollection(seed);
        seed.Add(Error());

        // Assert
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void HasErrors_TracksErrorSeverityOnly()
    {
        // Arrange
        _sut.Add(Warning());

        // Act & Assert
        _sut.HasErrors.ShouldBeFalse();
        _sut.Add(Error());
        _sut.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void ErrorsAndWarnings_AreSeverityViews()
    {
        // Arrange
        var info = Info();
        var warning = Warning();
        var error = Error();
        _sut.AddRange([info, warning, error]);

        // Act & Assert
        _sut.Errors.ShouldBe([error]);
        _sut.Warnings.ShouldBe([warning]);
    }

    [Fact]
    public void Demote_DowngradesEveryFindingAboveTheSeverity_InPlace()
    {
        // Arrange
        var info = Info();
        var warning = Warning();
        _sut.AddRange([info, warning, Error()]);

        // Act
        _sut.Demote(DiagnosticSeverity.Warning);

        // Assert — the error is now a warning; findings already at or below are untouched.
        _sut.HasErrors.ShouldBeFalse();
        _sut.Select(d => d.Severity).ShouldBe([DiagnosticSeverity.Info, DiagnosticSeverity.Warning, DiagnosticSeverity.Warning]);
        _sut[0].ShouldBe(info);
        _sut[1].ShouldBe(warning);
    }

    [Fact]
    public void Demote_PreservesTheDerivedDiagnosticType()
    {
        // Arrange
        var typed = new DiagnosticCollection<NsqlDiagnostic>(
            [new NsqlDiagnostic("syntax", "syntax-error", "boom", DiagnosticSeverity.Error, new SourcePosition(0, 1, 1))]);

        // Act
        typed.Demote(DiagnosticSeverity.Warning);

        // Assert
        typed.Single().Severity.ShouldBe(DiagnosticSeverity.Warning);
        typed.Single().Position.Line.ShouldBe(1);
    }

    [Fact]
    public void TypedCollection_FoldsUpward_AsTheBaseView()
    {
        // Arrange — covariance: a producer's typed collection is a base-typed view without translation.
        var error = new NsqlDiagnostic("syntax", "syntax-error", "boom", DiagnosticSeverity.Error, new SourcePosition(0, 1, 1));
        IDiagnosticCollection<Diagnostic> view = new DiagnosticCollection<NsqlDiagnostic>([error]);

        // Act & Assert
        view.HasErrors.ShouldBeTrue();
        view.Errors.ShouldBe([error]);
    }
}
