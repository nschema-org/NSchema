using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class ConsoleMessageTests
{
    [Fact]
    public void HighlightsInterpolatedValues_InMarkup_NotInPlainText()
    {
        // Arrange / Act — the literal text is left alone; the interpolated value becomes a highlighted hole.
        var package = "postgres";
        ConsoleMessage message = $"Restored {package} now";

        // Assert
        message.Styled.ShouldBe("Restored [bold]postgres[/] now");
        message.Plain.ShouldBe("Restored postgres now");
    }

    [Fact]
    public void EscapesMarkupCharactersInHoles()
    {
        // Arrange / Act — square brackets are Spectre markup delimiters, so the styled form escapes them; the plain
        // form keeps the raw text.
        var type = "text[]";
        ConsoleMessage message = $"Column {type}";

        // Assert
        message.Styled.ShouldBe("Column [bold]text[[]][/]");
        message.Plain.ShouldBe("Column text[]");
    }

    [Fact]
    public void AppliesFormatSpecifiersToHoles()
    {
        // Arrange / Act — a format specifier on a hole is honoured before highlighting.
        var when = new DateTimeOffset(2026, 6, 27, 9, 0, 0, TimeSpan.Zero);
        ConsoleMessage message = $"Since {when:u}";

        // Assert
        message.Plain.ShouldBe("Since 2026-06-27 09:00:00Z");
        message.Styled.ShouldBe("Since [bold]2026-06-27 09:00:00Z[/]");
    }
}
