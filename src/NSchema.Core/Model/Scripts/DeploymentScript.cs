namespace NSchema.Model.Scripts;

/// <summary>
/// A deployment bookend: the script runs on every apply, before or after the migration actions.
/// </summary>
/// <param name="Name">The name that identifies the script.</param>
/// <param name="Sql">The raw SQL to run.</param>
/// <param name="ScopeSchema">The schema the run is scoped to, or <see langword="null"/> when the script is global.</param>
/// <param name="Phase">Where the script runs relative to the migration actions.</param>
public sealed record DeploymentScript(
    SqlIdentifier Name,
    SqlText Sql,
    SqlIdentifier? ScopeSchema,
    DeploymentPhase Phase
) : Script(Name, Sql, ScopeSchema)
{
    /// <summary>
    /// When the script runs relative to deployments.
    /// </summary>
    public RunCondition RunCondition { get; init; } = RunCondition.Always;

    /// <inheritdoc />
    public override string Description => Phase == DeploymentPhase.Pre ? "PRE DEPLOYMENT" : "POST DEPLOYMENT";
}
