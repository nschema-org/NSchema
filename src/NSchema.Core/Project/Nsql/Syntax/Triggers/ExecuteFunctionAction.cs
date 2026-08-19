using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>EXECUTE FUNCTION|PROCEDURE [schema.]function(arguments)</c> — the function reference stays as
/// written (unqualified resolves via the engine's search path).
/// </summary>
/// <param name="Function">The function reference as written.</param>
/// <param name="Arguments">The argument list, verbatim (usually empty).</param>
public sealed record ExecuteFunctionAction(QualifiedName Function, SqlText Arguments) : TriggerAction
{
    /// <summary>
    /// The verbatim span of the whole action (<c>EXECUTE FUNCTION function(args)</c>) — filled by the parser or a factory.
    /// </summary>
    public Token ActionToken { get; init; } = Token.Missing;

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return ActionToken;
        }
    }
}
