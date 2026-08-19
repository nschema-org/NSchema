using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A top-level statement in an NSchema source file.
/// </summary>
public abstract record NsqlStatement : NsqlNode
{
    /// <summary>
    /// The documentation comment preceding the statement, if any.
    /// </summary>
    public string? Doc { get; init; }

    /// <summary>
    /// The doc-comment token preceding the statement, when parsed (carries its leading trivia for printing).
    /// </summary>
    public Token? DocComment { get; init; }
}
