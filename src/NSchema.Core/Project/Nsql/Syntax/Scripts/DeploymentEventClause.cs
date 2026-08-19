using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>ON PRE|POST DEPLOYMENT</c>.
/// </summary>
/// <param name="Phase">The deployment phase.</param>
public sealed record DeploymentEventClause(DeploymentPhase Phase) : ScriptEventClause
{
    /// <summary>
    /// The <c>PRE</c>/<c>POST</c> keyword token.
    /// </summary>
    public Token PhaseKeyword { get; init; } = Token.Keyword(Phase == DeploymentPhase.Pre ? NsqlKeywords.Pre : NsqlKeywords.Post);

    /// <summary>
    /// The <c>DEPLOYMENT</c> keyword token.
    /// </summary>
    public Token DeploymentKeyword { get; init; } = Token.Keyword(NsqlKeywords.Deployment);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return PhaseKeyword;
            yield return DeploymentKeyword;
        }
    }
}
