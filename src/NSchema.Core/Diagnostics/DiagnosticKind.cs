namespace NSchema.Diagnostics;

/// <summary>
/// What sort of finding this is.
/// </summary>
/// <remarks>
/// This decides whether enforcement may lower a finding's severity, and it is only consulted for errors.
/// A warning or an informational finding blocks nothing, so lowering one never changes the outcome.
/// </remarks>
public enum DiagnosticKind
{
    /// <summary>
    /// NSchema cannot do what was asked, and silencing it would not make the change work.
    /// </summary>
    Structural,

    /// <summary>
    /// NSchema can do what was asked and would do it correctly, the finding is a judgement about whether it should.
    /// </summary>
    Advisory
}
