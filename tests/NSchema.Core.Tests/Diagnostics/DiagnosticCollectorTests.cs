namespace NSchema.Tests.Diagnostics;

public sealed class DiagnosticCollectorTests
{
    private readonly DiagnosticCollector _sut = new();

    private static Diagnostic Error(string message = "boom") => Diagnostic.Error("source", "error", message);

    private static Diagnostic Warning(string message = "careful") => Diagnostic.Warning("source", "warning", message);

    // -------------------------------------------------------------------------
    // Add
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_CollectsFindings_InInsertionOrder()
    {
        // Arrange
        var warning = Warning();
        var error = Error();

        // Act
        _sut.AddRange(warning);
        _sut.AddRange([error]);

        // Assert
        _sut.ShouldBe([warning, error]);
    }

    [Fact]
    public void Add_AbsorbsAResultsDiagnostics()
    {
        // Arrange
        var warning = Warning();

        // Act
        _sut.AddRange(Result.Success("value", warning));

        // Assert
        _sut.ShouldBe([warning]);
    }

    [Fact]
    public void HasErrors_TracksErrorSeverityOnly()
    {
        // Arrange
        _sut.AddRange(Warning());

        // Act & Assert
        _sut.HasErrors.ShouldBeFalse();
        _sut.AddRange(Error());
        _sut.HasErrors.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // TryTake
    // -------------------------------------------------------------------------

    [Fact]
    public void TryTake_OnSuccess_HandsBackTheValue_AndAbsorbsDiagnostics()
    {
        // Arrange
        var warning = Warning();
        var result = Result.Success("payload", warning);

        // Act
        var taken = _sut.TryTake(result, out var value);

        // Assert
        taken.ShouldBeTrue();
        value.ShouldBe("payload");
        _sut.ShouldBe([warning]);
    }

    [Fact]
    public void TryTake_OnValuelessFailure_ReturnsFalse_AndAbsorbsDiagnostics()
    {
        // Arrange
        var error = Error();
        var result = Result.Failure<string>(error);

        // Act
        var taken = _sut.TryTake(result, out _);

        // Assert
        taken.ShouldBeFalse();
        _sut.ShouldBe([error]);
    }

    [Fact]
    public void TryTake_GuardsOnValuePresence_NotSuccess()
    {
        // Arrange — a failure that still carries its best-effort value.
        var error = Error();
        var result = Result.From("partial", [error]);

        // Act
        var taken = _sut.TryTake(result, out var value);

        // Assert
        taken.ShouldBeTrue();
        value.ShouldBe("partial");
        _sut.ShouldBe([error]);
    }

    // -------------------------------------------------------------------------
    // Require
    // -------------------------------------------------------------------------

    [Fact]
    public void Require_ReturnsTheValue_AndAbsorbsDiagnostics()
    {
        // Arrange
        var warning = Warning();

        // Act
        var value = _sut.Require(Result.Success("payload", warning));

        // Assert
        value.ShouldBe("payload");
        _sut.ShouldBe([warning]);
    }

    [Fact]
    public void Require_OnValuelessResult_Throws()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _sut.Require(Result.Failure<string>(Error())));
    }

    // -------------------------------------------------------------------------
    // ToResult
    // -------------------------------------------------------------------------

    [Fact]
    public void ToResult_WithNoErrors_IsSuccess_CarryingEverythingCollected()
    {
        // Arrange
        var warning = Warning();
        _sut.AddRange(warning);

        // Act
        var result = _sut.ToResult();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBe([warning]);
    }

    [Fact]
    public void ToResult_WithAnError_IsFailure()
    {
        // Arrange
        var error = Error();
        _sut.AddRange(error);

        // Act
        var result = _sut.ToResult();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
    }

    [Fact]
    public void ToResultT_CarriesTheValue_WhetherOrNotFailed()
    {
        // Arrange
        var error = Error();
        _sut.AddRange(error);

        // Act
        var result = _sut.ToResult("partial");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBe("partial");
        result.Diagnostics.ShouldBe([error]);
    }

    [Fact]
    public void ToResult_SnapshotsTheDiagnostics_LaterAddsDoNotLeakIn()
    {
        // Arrange
        var warning = Warning();
        _sut.AddRange(warning);

        // Act
        var result = _sut.ToResult();
        _sut.AddRange(Error());

        // Assert
        result.Diagnostics.ShouldBe([warning]);
    }
}
