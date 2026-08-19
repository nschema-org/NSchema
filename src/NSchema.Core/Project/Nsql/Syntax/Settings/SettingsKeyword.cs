namespace NSchema.Project.Nsql.Syntax.Settings;

/// <summary>
/// The keyword a settings statement leads with, identifying what it declares.
/// </summary>
public enum SettingsKeyword
{
    /// <summary>
    /// <c>PLUGIN</c> — declares a plugin dependency.
    /// </summary>
    Plugin,

    /// <summary>
    /// <c>ENGINE</c> — asserts the engine (or host) version.
    /// </summary>
    Engine,

    /// <summary>
    /// <c>DATABASE</c> — selects and configures the database.
    /// </summary>
    Database,

    /// <summary>
    /// <c>STATE</c> — selects and configures where state is stored.
    /// </summary>
    State,

    /// <summary>
    /// <c>LOCK</c> — records a resolved plugin pin (lockfile only).
    /// </summary>
    Lock,
}
