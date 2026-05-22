using NSchema.Schema;

namespace NSchema.Migration.Plan;

/// <summary>
/// Represents running a pre-deployment script as part of the database migration process.
/// </summary>
/// <param name="Script">The SQL script to be executed before the main deployment steps are performed.</param>
/// <remarks>
/// This action allows executing custom SQL scripts before the main deployment steps are performed,
/// which can be useful for tasks such as preparing the database environment, performing data transformations,
/// or setting up necessary conditions for the subsequent migration actions.
/// </remarks>
public sealed record RunPreDeploymentScript(Script Script) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
