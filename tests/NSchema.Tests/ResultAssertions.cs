namespace NSchema.Tests;

/// <summary>
/// Asserts on the shape every expected CLI failure now takes: a failed <see cref="Result"/> carrying error
/// diagnostics, rather than a thrown exception.
/// </summary>
internal static class ResultAssertions
{
    extension(Result result)
    {
        /// <summary>
        /// Asserts the result failed and that some error diagnostic's message contains <paramref name="expected"/>.
        /// Case-insensitive by default, matching Shouldly's own <c>ShouldContain</c> for strings.
        /// </summary>
        public void ShouldFailContaining(string expected, Case sensitivity = Case.Insensitive)
        {
            result.IsFailure.ShouldBeTrue($"Expected a failure, but the result succeeded. Diagnostics: {Describe(result)}");
            result.Errors.ShouldContain(
                error => error.Message.Contains(expected, sensitivity == Case.Sensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
                $"No error mentioned '{expected}'. Diagnostics: {Describe(result)}");
        }
    }

    private static string Describe(Result result) =>
        result.Diagnostics.Count == 0 ? "(none)" : string.Join("; ", result.Diagnostics.Select(d => $"[{d.Severity}] {d.Source}: {d.Message}"));
}
