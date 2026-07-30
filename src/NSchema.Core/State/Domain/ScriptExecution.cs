using NSchema.Model.Scripts;

namespace NSchema.State.Domain;

/// <summary>
/// A recorded script execution.
/// </summary>
/// <param name="Script">The address of the script that was executed.</param>
/// <param name="Hash">The hash of the script body that was executed.</param>
/// <param name="ExecutedUtc">When the execution was recorded.</param>
public sealed record ScriptExecution(ScriptReference Script, ScriptHash Hash, DateTimeOffset ExecutedUtc);
