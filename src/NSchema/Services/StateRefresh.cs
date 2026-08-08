using NSchema.Operations;

namespace NSchema.Services;

/// <summary>
/// The capture a command takes before it plans.
/// </summary>
internal static class StateRefresh
{
    /// <summary>
    /// Captures the live schema into the state store.
    /// </summary>
    /// <param name="operations">The operations to capture through.</param>
    /// <param name="reporter">The reporter to render the capture's diagnostics through.</param>
    /// <param name="skip">Whether the capture was declined (<c>--no-refresh</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Whether the command may go on to plan.</returns>
    public static async Task<bool> TryRefresh(INSchemaOperations operations, IConsoleReporter reporter, bool skip, CancellationToken cancellationToken)
    {
        if (skip)
        {
            return true;
        }

        // Not forced: a forced capture resets the run-once ledger, and an unattended run is exactly where that
        // would go unnoticed. An unreadable payload stops the run instead.
        var refreshed = await operations.Refresh(new RefreshArguments(), cancellationToken);
        if (refreshed.Diagnostics.Count > 0)
        {
            reporter.ReportDiagnostics(refreshed.Diagnostics);
        }

        return refreshed.IsSuccess;
    }
}
