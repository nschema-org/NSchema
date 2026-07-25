namespace NSchema.Tests.Diagnostics;

public sealed class FormattedTextTests
{
    [Fact]
    public void Interpolation_MarksHolesAsValues()
    {
        FormattedText text = $"Table {"users"} is already declared.";

        text.Spans.ShouldBe([
            new FormattedText.Span("Table ", false),
            new FormattedText.Span("users", true),
            new FormattedText.Span(" is already declared.", false),
        ]);
    }

    [Fact]
    public void Interpolation_TextFormat_MarksTheHoleAsProse()
    {
        FormattedText text = $"the {"composite type":text} '{"point"}'";

        // The prose hole merges into the surrounding literals; only the identifier is a value.
        text.Spans.ShouldBe([
            new FormattedText.Span("the composite type '", false),
            new FormattedText.Span("point", true),
            new FormattedText.Span("'", false),
        ]);
    }

    [Fact]
    public void Interpolation_SplicesNestedFormattedText()
    {
        // Arrange
        FormattedText inner = $"table '{"users"}'";

        // Act
        FormattedText outer = $"context: {inner} (at 3:14).";

        // Assert
        outer.ToString().ShouldBe("context: table 'users' (at 3:14).");
        outer.Spans.Count(s => s.IsValue).ShouldBe(1);
        outer.Spans.Single(s => s.IsValue).Text.ShouldBe("users");
    }

    [Fact]
    public void ImplicitConversion_WrapsPlainTextAsOneLiteral()
    {
        FormattedText text = "No SQL DDL files matched.";

        text.Spans.ShouldBe([new FormattedText.Span("No SQL DDL files matched.", false)]);
    }

    [Fact]
    public void Equality_IsByContentIncludingSpanKinds()
    {
        FormattedText fromValues = $"drop of {"users"}";
        FormattedText sameValues = $"drop of {"users"}";
        FormattedText flattened = "drop of users";

        fromValues.ShouldBe(sameValues);
        fromValues.ShouldNotBe(flattened); // same text, but the value span is part of the content
        fromValues.GetHashCode().ShouldBe(sameValues.GetHashCode());
    }

    [Fact]
    public void ToString_IsThePlainText()
    {
        FormattedText text = $"Schema '{"app"}' is already declared.";

        text.ToString().ShouldBe("Schema 'app' is already declared.");
    }
}
