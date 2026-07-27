using NSchema.Services.Reporting;

namespace NSchema.Extensions;

/// <summary>
/// Folds the CLI's "render the reasons, then bail" step into a single guard, so every command reports an expected
/// failure the same way.
/// </summary>
internal static class ResultExtensions
{
    extension(Result result)
    {
        /// <summary>
        /// Reports every diagnostic the result carries — of any severity, so advisories survive a success — and
        /// returns whether it failed.
        /// </summary>
        /// <param name="messenger">The messenger to render through.</param>
        /// <returns><see langword="true"/> when the result is a failure and the caller should stop.</returns>
        public bool ReportFailure(IConsoleMessenger messenger)
        {
            if (result.Diagnostics.Count > 0)
            {
                messenger.ReportDiagnostics(result.Diagnostics);
            }

            return result.IsFailure;
        }
    }
}
