namespace NSchema.Services;

/// <summary>
/// Carries side-band facts about a run that the command layer needs after the operation completes.
/// </summary>
internal sealed class RunOutcome
{
    /// <summary>
    /// Whether the run reported a non-empty diff (pending changes / detected drift).
    /// </summary>
    public bool HasChanges { get; set; }
}
