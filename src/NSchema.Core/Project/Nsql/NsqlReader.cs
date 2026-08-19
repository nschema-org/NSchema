using NSchema.Model;

namespace NSchema.Project.Nsql;

/// <summary>
/// Reads NSchema source into the syntax tree. One grammar, one reader: declarations, directives, and
/// configuration/lock blocks all parse into one <see cref="NsqlDocument"/>. Which statements a given file
/// <em>should</em> hold is a consumer's concern — each subsystem picks out the statements it understands.
/// </summary>
public static class NsqlReader
{
    /// <summary>
    /// Reads raw <paramref name="source"/> into an <see cref="NsqlDocument"/>. The parser recovers at
    /// statement boundaries, so every syntax error is reported at once; the document carries the statements
    /// that parsed, even on a failure.
    /// </summary>
    /// <param name="source">The NSchema source text.</param>
    public static Result<NsqlDocument, NsqlDiagnostic> Read(string source)
    {
        var parser = new NsqlParser(source);
        var document = parser.Parse();
        return Result<NsqlDocument, NsqlDiagnostic>.From(document, parser.Errors.Select(NsqlDiagnostics.Syntax).ToList());
    }

    /// <summary>
    /// Reads a single address fragment, resolving quoted segments.
    /// </summary>
    /// <param name="source">The address text, e.g. a CLI scope target.</param>
    public static Result<Address, NsqlDiagnostic> ReadAddress(string source)
    {
        var parser = new NsqlParser(source);
        try
        {
            return parser.ParseAddress();
        }
        catch (NsqlSyntaxException exception)
        {
            return NsqlDiagnostics.Syntax(exception);
        }
    }

    /// <summary>
    /// Reads the file at <paramref name="path"/> into an <see cref="NsqlDocument"/>, stamping the path onto
    /// the document and every diagnostic. An unreadable file is an error diagnostic, not an exception.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<NsqlDocument, NsqlDiagnostic>> ReadFile(string path, CancellationToken cancellationToken = default)
    {
        string source;
        try
        {
            source = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return NsqlDiagnostics.UnreadableFile(path, exception);
        }

        var result = Read(source);
        return Result<NsqlDocument, NsqlDiagnostic>.From(
            result.Value is { } document ? document with { FilePath = path } : null,
            [.. result.Diagnostics.Select(d => d with { File = path })]
        );
    }
}
