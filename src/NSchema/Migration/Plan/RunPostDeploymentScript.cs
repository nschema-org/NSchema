using NSchema.Schema;

namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the action of running a post-deployment script as part of a database migration process.
/// </summary>
/// <param name="Script">The SQL script to be executed after the main deployment steps have been completed.</param>
/// <remarks>
/// This action allows for executing custom SQL scripts after the main deployment steps have been completed,
/// enabling additional configuration or data manipulation that may be necessary for the application to function correctly after deployment.
/// </remarks>
public sealed record RunPostDeploymentScript(Script Script) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
