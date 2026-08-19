using System.Text;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// One constituent of a node in source order: either a <see cref="Token"/> or a child <see cref="NsqlNode"/>.
/// The printer walks a node's children to reproduce its source.
/// </summary>
internal readonly struct NsqlChild
{
    private readonly NsqlNode? _node;
    private readonly Token _token;

    private NsqlChild(NsqlNode node)
    {
        _node = node;
        _token = default;
    }

    private NsqlChild(Token token)
    {
        _node = null;
        _token = token;
    }

    public static implicit operator NsqlChild(NsqlNode node) => new(node);
    public static implicit operator NsqlChild(Token token) => new(token);

    /// <summary>
    /// Where this child begins in the source (the node's computed position, or the token's).
    /// </summary>
    public SourcePosition Position => _node?.Position ?? _token.Position;

    /// <summary>
    /// The token this child is, or <see langword="null"/> when it is a node.
    /// </summary>
    public Token? AsToken() => _node is null ? _token : null;

    /// <summary>
    /// The node this child is, or <see langword="null"/> when it is a token.
    /// </summary>
    public NsqlNode? AsNode() => _node;

    public void WriteTo(StringBuilder sb)
    {
        if (_node is { } node)
        {
            node.WriteTo(sb);
        }
        else
        {
            _token.WriteTo(sb);
        }
    }
}
