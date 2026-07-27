using System.Reflection;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class ExceptionReportTests
{
    [Fact]
    public void Describe_JoinsTheInnerExceptionChain()
    {
        // Arrange
        // The shape a provider failure arrives in: the outer type names the operation, the inner one says what
        // actually went wrong, and only the inner message is any use to the reader.
        var exception = new InvalidOperationException("Failed to connect to 127.0.0.1:5432",
            new IOException("Connection refused"));

        // Act
        var described = ExceptionReport.Describe(exception);

        // Assert
        described.ShouldBe("Failed to connect to 127.0.0.1:5432 -> Connection refused");
    }

    [Fact]
    public void Describe_SkipsInnerMessagesTheOuterAlreadyQuotes()
    {
        // Arrange
        // Wrapping exceptions habitually repeat their inner message, which would otherwise print twice.
        var inner = new IOException("Connection refused");
        var exception = new InvalidOperationException($"Plugin failed: {inner.Message}", inner);

        // Act / Assert
        ExceptionReport.Describe(exception).ShouldBe("Plugin failed: Connection refused");
    }

    [Fact]
    public void Describe_UnwrapsSingleExceptionAggregates() =>
        ExceptionReport.Describe(new AggregateException(new InvalidOperationException("Bad flag.")))
            .ShouldBe("Bad flag.");

    [Fact]
    public void Describe_KeepsAggregatesCarryingSeveralFailures()
    {
        // Arrange
        // The aggregate's own message is the only thing accounting for every failure, so it is kept.
        var exception = new AggregateException(new IOException("First"), new IOException("Second"));

        // Act
        var described = ExceptionReport.Describe(exception);

        // Assert
        described.ShouldContain("First");
        described.ShouldContain("Second");
    }

    [Fact]
    public void TypeName_ReportsTheUnwrappedType() =>
        ExceptionReport.TypeName(new TargetInvocationException(new NullReferenceException()))
            .ShouldBe(nameof(NullReferenceException));

    [Fact]
    public void Stack_KeepsNSchemaFramesAndDropsPlumbing()
    {
        // Arrange
        Exception caught;
        try
        {
            throw new InvalidOperationException("Boom!");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        // Act
        var stack = ExceptionReport.Stack(caught);

        // Assert
        stack.ShouldContain(nameof(Stack_KeepsNSchemaFramesAndDropsPlumbing));
        stack.ShouldNotContain("at System.Runtime.CompilerServices.");
        stack.ShouldNotContain("--- End of stack trace from previous location ---");
    }
}
