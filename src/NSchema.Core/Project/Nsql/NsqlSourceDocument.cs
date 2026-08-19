using System.Text;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed NSchema source document.
/// </summary>
public abstract record NsqlSourceDocument
{
    /// <summary>
    /// The file the document was read from, or <see langword="null"/> when parsed from raw source.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// The end-of-file token, when parsed; its leading trivia is the file's trailing whitespace and comments.
    /// </summary>
    public Token? EndOfFile { get; init; }

    /// <summary>
    /// The document's statements, as syntax nodes, for the printer.
    /// </summary>
    private protected abstract IReadOnlyList<NsqlStatement> StatementNodes { get; }

    /// <summary>
    /// Reprints the document as source text.
    /// </summary>
    public string ToSource()
    {
        var sb = new StringBuilder();
        foreach (var statement in StatementNodes)
        {
            statement.WriteTo(sb);
        }
        EndOfFile?.WriteTo(sb);
        return sb.ToString();
    }
}
