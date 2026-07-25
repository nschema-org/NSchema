namespace NSchema.Operations;

/// <summary>
/// Represents a single workflow operation.
/// </summary>
/// <typeparam name="TArgs">The arguments the operation runs with.</typeparam>
/// <typeparam name="TResult">The operation's result (a <see cref="Result"/> or <c>Result&lt;T&gt;</c>).</typeparam>
internal interface IOperation<in TArgs, TResult>
{
    /// <summary>
    /// Runs the operation.
    /// </summary>
    /// <param name="args">The arguments controlling the operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<TResult> Execute(TArgs args, CancellationToken cancellationToken = default);
}
