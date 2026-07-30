namespace NSchema.Tests.Diagnostics;

public sealed class ResultTests
{
    private static Diagnostic Error(string message = "boom") => Diagnostic.Error("source", "error", message);

    private static Diagnostic Warning(string message = "careful") => Diagnostic.Warning("source", "warning", message);

    // -------------------------------------------------------------------------
    // Diagnostic factories
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(DiagnosticSeverity.Info)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Error)]
    public void Diagnostic_Factories_SetSourceMessageAndSeverity(DiagnosticSeverity severity)
    {
        // Act
        var diagnostic = severity switch
        {
            DiagnosticSeverity.Info => Diagnostic.Info("cfg", "msg", "msg"),
            DiagnosticSeverity.Warning => Diagnostic.Warning("cfg", "msg", "msg"),
            _ => Diagnostic.Error("cfg", "msg", "msg"),
        };

        // Assert
        diagnostic.Source.ShouldBe("cfg");
        diagnostic.Message.ShouldBe("msg");
        diagnostic.Severity.ShouldBe(severity);
    }

    // -------------------------------------------------------------------------
    // Result<Success> (the no-value result)
    // -------------------------------------------------------------------------

    [Fact]
    public void Result_Success_IsSuccessful_AndCarriesAdvisoryDiagnostics()
    {
        // Arrange
        var warning = Warning();

        // Act
        var result = Result.Success("value", warning);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Diagnostics.ShouldBe([warning]);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Result_Failure_IsNotSuccessful_AndExposesErrorSubset()
    {
        // Arrange
        var warning = Warning();
        var error = Error();

        // Act
        var result = Result.Failure<string>(warning, error);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldBe([warning, error]);
        result.Errors.ShouldBe([error]);
    }

    [Fact]
    public void Result_ExposesSeverityViews_OnItselfAndItsDiagnostics()
    {
        // Arrange
        var warning = Warning();
        var error = Error();

        // Act
        var result = Result.From([warning, error]);

        // Assert
        result.Warnings.ShouldBe([warning]);
        result.Diagnostics.HasErrors.ShouldBeTrue();
        result.Diagnostics.Errors.ShouldBe([error]);
    }

    // -------------------------------------------------------------------------
    // Result<T> — value presence tracks success
    // -------------------------------------------------------------------------

    [Fact]
    public void ResultT_Success_ExposesValue()
    {
        // Act
        var result = Result.Success<string>("payload");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("payload");
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ResultT_Failure_HasNoValue_AndExposesErrors()
    {
        // Arrange
        var error = Error();

        // Act
        var result = Result.Failure<string>(error);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        result.Errors.ShouldBe([error]);
    }

    // -------------------------------------------------------------------------
    // Implicit conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ResultT_ImplicitFromValue_IsSuccess()
    {
        // Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void ResultT_ImplicitFromDiagnostic_IsFailure()
    {
        // Arrange
        var error = Error();

        // Act
        Result<int> result = error;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
    }

    // -------------------------------------------------------------------------
    // Map
    // -------------------------------------------------------------------------

    [Fact]
    public void Map_ProjectsSuccessValue_AndPropagatesDiagnostics()
    {
        // Arrange
        var warning = Warning();
        var result = Result.Success(3, warning);

        // Act
        var mapped = result.Map(value => value * 2);

        // Assert
        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBe(6);
        mapped.Diagnostics.ShouldBe([warning]);
    }

    [Fact]
    public void Map_OnFailureWithValue_ProjectsTheCarriedValue()
    {
        // Arrange
        var error = Error();
        var result = Result.From<int?>(3, [error]);

        // Act
        var mapped = result.Map(value => value * 2);

        // Assert
        mapped.IsFailure.ShouldBeTrue();
        mapped.Value.ShouldBe(6);
        mapped.Errors.ShouldBe([error]);
    }

    [Fact]
    public void Map_OnValuelessFailure_PassesDiagnosticsThrough_WithoutInvokingProjection()
    {
        // Arrange
        var error = Error();
        var result = Result.Failure<string>(error);
        var invoked = false;

        // Act
        var mapped = result.Map(value => { invoked = true; return value.Length; });

        // Assert
        invoked.ShouldBeFalse();
        mapped.IsFailure.ShouldBeTrue();
        mapped.Errors.ShouldBe([error]);
    }
}
