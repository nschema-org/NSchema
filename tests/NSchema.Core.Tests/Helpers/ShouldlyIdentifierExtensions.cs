using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Shouldly bridges for <see cref="ValueObject"/>s: asserting a value object against a string literal compares
/// the exact underlying text, so tests keep witnessing that casing is preserved end-to-end.
/// </summary>
internal static class ShouldlyIdentifierExtensions
{
    extension<T>(ValueObject<T>? actual)
    {
        // A null receiver still asserts (against default), so a missing value fails rather than silently passing.
        public void ShouldBe(T? expected) => (actual is null ? default : actual.Value).ShouldBe(expected);
    }

    extension(RoutineReference? actual)
    {
        public void ShouldBe(string? expected) => (actual?.ToString()).ShouldBe(expected);
    }

    extension(DatabaseAddress? actual)
    {
        public void ShouldBe(string? expected) => (actual?.Name.Value).ShouldBe(expected);
    }
}
