using NSchema.Commands.Lock.Acquire;

namespace NSchema.Tests.Commands.Lock.Acquire;

public sealed class DurationTests
{
    [Theory]
    [InlineData("90s", 90)]
    [InlineData("30m", 30 * 60)]
    [InlineData("2h", 2 * 60 * 60)]
    [InlineData("1d", 24 * 60 * 60)]
    [InlineData("1H", 60 * 60)] // unit is case-insensitive
    public void TryParse_ParsesShortDurations(string text, int expectedSeconds)
    {
        // Act
        var parsed = Duration.TryParse(text, out var value);

        // Assert
        parsed.ShouldBeTrue();
        value.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void TryParse_FallsBackToTimeSpanFormat()
    {
        // Act
        var parsed = Duration.TryParse("00:30:00", out var value);

        // Assert
        parsed.ShouldBeTrue();
        value.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("10x")]    // unknown unit
    [InlineData("0m")]     // must be positive
    [InlineData("-5m")]    // negative is not a valid count
    [InlineData("1.5h")]   // no fractional units
    public void TryParse_RejectsInvalidDurations(string text)
    {
        // Act
        var parsed = Duration.TryParse(text, out _);

        // Assert
        parsed.ShouldBeFalse();
    }
}
