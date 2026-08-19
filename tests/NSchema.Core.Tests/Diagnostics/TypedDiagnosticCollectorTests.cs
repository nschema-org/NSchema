using NSchema.Project.Nsql;

namespace NSchema.Tests.Diagnostics;

/// <summary>
/// A collector over a specialized diagnostic type: it accumulates and folds without widening to the base
/// <see cref="Diagnostic"/>, so a producer that mints structured findings keeps them structured.
/// </summary>
public sealed class TypedDiagnosticCollectorTests
{
    private readonly DiagnosticCollector<NsqlDiagnostic> _sut = new();

    private static NsqlDiagnostic Error(string message = "boom") =>
        new("syntax", "error", message, DiagnosticSeverity.Error, new SourcePosition(Offset: 0, Line: 1, Column: 1));

    private static NsqlDiagnostic Warning(string message = "careful") =>
        new("syntax", "warning", message, DiagnosticSeverity.Warning, new SourcePosition(Offset: 12, Line: 2, Column: 1));

    [Fact]
    public void Add_AbsorbsATypedResultsDiagnostics()
    {
        // Act
        _sut.Add(Result<string, NsqlDiagnostic>.Failure(Error()));

        // Assert
        _sut.ShouldHaveSingleItem().Message.ShouldBe("boom");
    }

    [Fact]
    public void TryTake_OnSuccess_HandsBackTheValue_AndAbsorbsDiagnostics()
    {
        // Act
        var taken = _sut.TryTake(Result<string, NsqlDiagnostic>.Success("value", Warning()), out var value);

        // Assert
        taken.ShouldBeTrue();
        value.ShouldBe("value");
        _sut.ShouldHaveSingleItem().Message.ShouldBe("careful");
    }

    [Fact]
    public void ToResult_KeepsTheDiagnosticsTyped()
    {
        // Arrange
        _sut.Add(Warning());

        // Act
        var result = _sut.ToResult("value");

        // Assert — the position rides through, which is the point of the specialized type.
        result.Value.ShouldBe("value");
        result.Diagnostics.ShouldHaveSingleItem().Position.Line.ShouldBe(2);
    }

    [Fact]
    public void ToResult_FoldsUpwardAsAPlainResult()
    {
        // Arrange
        _sut.Add(Error());

        // Act — a caller that does not care about the specialized type sees an ordinary failure.
        Result<string> folded = _sut.ToResult("value");

        // Assert
        folded.IsFailure.ShouldBeTrue();
        folded.Errors.ShouldHaveSingleItem().Message.ShouldBe("boom");
    }

    [Fact]
    public void CollectionExpression_SeedsACollection()
    {
        // Act
        DiagnosticCollection<NsqlDiagnostic> diagnostics = [Warning(), Error()];

        // Assert
        diagnostics.Count.ShouldBe(2);
        diagnostics.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccess()
    {
        // Act
        Result<string, NsqlDiagnostic> result = "value";

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("value");
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ImplicitConversion_FromDiagnostic_IsFailure()
    {
        // Act
        Result<string, NsqlDiagnostic> result = Error();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Position.Line.ShouldBe(1);
    }

    [Fact]
    public void ImplicitConversion_FromDiagnostic_ToValuelessResult_IsFailure()
    {
        // Act
        Result result = Diagnostic.Error("source", "boom", "boom");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("boom");
    }
}
