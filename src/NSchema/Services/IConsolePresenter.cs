using NSchema.Operations;

namespace NSchema.Services;

/// <summary>
/// The CLI's single presentation surface: a superset of the core <see cref="IOperationReporter"/>.
/// </summary>
internal interface IConsolePresenter : IOperationReporter
{
    /// <summary>
    /// Writes an indented secondary line beneath a headline (e.g. the lock id and expiry under a <c>lock status</c> line).
    /// </summary>
    void Detail(string message);
}
