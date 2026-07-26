using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Reads and writes the plugin lockfile (<c>nschema.lock</c>). Static rather than a DI service: the lockfile is
/// a single file at a caller-supplied path, with no per-application state to hold. Core owns the format both
/// ways, so a written lockfile always round-trips.
/// </summary>
public static class LockFileManager
{
    private const string Source = "lockfile";

    private const string Header = "-- nschema.lock — managed by NSchema. Do not edit by hand; regenerate it instead.";

    /// <summary>
    /// Reads the lockfile at <paramref name="path"/>. A missing file is an empty lockfile (nothing locked yet),
    /// not an error; unknown attributes are ignored, so a lockfile written by a newer NSchema still loads.
    /// </summary>
    /// <param name="path">The lockfile path.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<LockFile>> Read(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Result.Success(LockFile.Empty);
        }

        var document = await NsqlReader.ReadFile(path, cancellationToken);
        var diagnostics = new List<Diagnostic>(document.Diagnostics);
        var plugins = new List<LockedPlugin>();

        if (document.Value is { } value)
        {
            foreach (var statement in value.Statements.OfType<SettingsStatement>())
            {
                var result = statement.ToSettings().Get<LockedPlugin>(ignoreUnknown: true);
                diagnostics.AddRange(result.Diagnostics);

                if (result.IsSuccess)
                {
                    plugins.Add(result.Require());
                }
            }
        }

        return Result.From(new LockFile(plugins), diagnostics);
    }

    /// <summary>
    /// Writes <paramref name="lockFile"/> to <paramref name="path"/> as canonical lockfile source — one
    /// <c>LOCK</c> statement per pin, in order.
    /// </summary>
    /// <param name="path">The lockfile path.</param>
    /// <param name="lockFile">The lockfile to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result> Write(string path, LockFile lockFile, CancellationToken cancellationToken = default)
    {
        // Build a LOCK statement per pin and let the writer render them, so the lockfile takes the same one path to
        // canonical source as everything else. The header stays outside the document because a lockfile with nothing
        // pinned still carries it, and there would be no statement to hang it on.
        var statements = lockFile.Plugins
            .Select(plugin => SettingsStatement.Lock()
                .WithSetting("source", plugin.Source.ToString())
                .WithSetting("version", plugin.Version.ToString())
            ).ToList();
        var document = new NsqlDocument(statements);
        var text = Header + "\n" + NsqlWriter.Write(document);

        try
        {
            await File.WriteAllTextAsync(path, text, cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.From(Diagnostic.Error(Source, $"Could not write the lockfile '{path}': {exception.Message:text}"));
        }
    }
}
