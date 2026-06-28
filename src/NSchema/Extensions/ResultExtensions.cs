using NSchema.Diagnostics;

namespace NSchema.Extensions;

/// <summary>
/// Bridges a <see cref="Result{T}"/> back to an exception for the fail-fast commands.
/// </summary>
internal static class ResultExtensions
{
    extension<T>(Result<T> result)
    {
        /// <summary>
        /// Returns the value of a successful result, or throws with the joined error messages on failure.
        /// </summary>
        public T ValueOrThrow() => result.IsSuccess ? result.Value : throw Fail(result.Errors);

        /// <summary>
        /// Throws with the joined error messages when the result is a failure; otherwise does nothing.
        /// </summary>
        public void ThrowIfFailure()
        {
            if (result.IsFailure)
            {
                throw Fail(result.Errors);
            }
        }
    }

    private static InvalidOperationException Fail(IEnumerable<Diagnostic> errors) =>
        new(string.Join(Environment.NewLine, errors.Select(e => e.Message)));
}
