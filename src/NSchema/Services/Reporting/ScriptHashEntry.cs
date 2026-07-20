namespace NSchema.Services.Reporting;

/// <summary>
/// A declared script's name and body hash, as the state ledger records them.
/// </summary>
internal sealed record ScriptHashEntry(string Name, string Hash);
