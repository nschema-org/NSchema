using System.CommandLine;
using NSchema.Configuration;
using NSchema.Project.Nsql;

namespace NSchema.Commands.Format;

internal static class FormatCommand
{
    private static readonly Argument<string> PathArgument = new("path")
    {
        Description = "A .sql file or a directory to format (recursively), or '-' to read stdin and write stdout. " +
                      "Defaults to the current directory.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ".",
    };

    private static readonly Option<bool> CheckOption = new("--check")
    {
        Description = "Don't write changes; list the files that need formatting and exit 2 if any do.",
    };

    /// <summary>
    /// The presentation flags <c>format</c> cannot honour. Its whole output surface is a payload for other tools —
    /// the formatted source, the <c>--check</c> file list piped into the next command, and compiler-style
    /// <c>path(line,col): message</c> errors an editor or CI problem matcher parses. None of that varies by format or
    /// verbosity, so accepting these silently would promise a rendering that never happens.
    /// </summary>
    private static readonly (Option Option, string Name)[] UnsupportedPresentationOptions =
    [
        (CommonOptions.Json.Option, "--json"),
        (CommonOptions.Format.Option, "--format"),
        (CommonOptions.Quiet.Option, "--quiet"),
        (CommonOptions.Verbose.Option, "--verbose"),
    ];

    public static Command Create()
    {
        var command = new Command("format", "Reformat .sql DDL files to a canonical layout (in place, or check with --check).");
        command.Arguments.Add(PathArgument);
        command.Options.Add(CheckOption);

        // Rejected while parsing, like the other harness-flag usage errors: the alternative is honouring them
        // silently, which reads as "this ran the way I asked" when it did not.
        command.Validators.Add(result =>
        {
            foreach (var (option, name) in UnsupportedPresentationOptions)
            {
                if (result.Specified(option))
                {
                    result.AddError($"{name} cannot be used with 'format': its output is a payload for other tools, not a report.");
                }
            }
        });

        command.SetAction(Run);
        return command;
    }

    private static int Run(ParseResult parseResult)
    {
        var path = parseResult.GetValue(PathArgument)!;
        var check = parseResult.GetValue(CheckOption);

        if (path == "-")
        {
            return FormatStdin(check);
        }

        var changed = FormatPath(path, check);
        if (changed.IsFailure)
        {
            // Reported like the syntax errors above rather than through the reporter: format is a source-text tool,
            // and its whole output surface is compiler-style lines on stdout/stderr.
            foreach (var error in changed.Errors)
            {
                Console.Error.WriteLine(error.Message);
            }

            return ExitCodes.Error;
        }

        foreach (var file in changed.Require())
        {
            Console.Out.WriteLine(file);
        }
        return check && changed.Require().Count > 0 ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }

    private static int FormatStdin(bool check)
    {
        var input = Console.In.ReadToEnd();
        if (Format(input, "<stdin>") is not { } formatted)
        {
            return ExitCodes.Error;
        }
        if (check)
        {
            return formatted == input ? ExitCodes.NoChanges : ExitCodes.HasChanges;
        }
        Console.Out.Write(formatted);
        return ExitCodes.NoChanges;
    }

    /// <summary>
    /// The canonical formatting of <paramref name="source"/>, or <see langword="null"/> when it does not parse —
    /// a syntax error is reported rather than rewritten over, since the render would not be the whole document.
    /// </summary>
    private static string? Format(string source, string origin)
    {
        var result = NsqlWriter.Format(source);
        if (result.IsFailure)
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{origin}({error.Position.Line},{error.Position.Column}): {error.Message}");
            }
            return null;
        }
        return result.Value;
    }

    /// <summary>
    /// Formats every <c>.sql</c> file under <paramref name="path"/> (a single file or a directory tree). Returns the
    /// files whose content changed; when <paramref name="check"/> is <see langword="false"/> those files are rewritten.
    /// </summary>
    /// <returns>The changed files, or a failure when <paramref name="path"/> names nothing.</returns>
    internal static Result<IReadOnlyList<string>> FormatPath(string path, bool check)
    {
        var files = ResolveFiles(path);
        if (files.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(files.Diagnostics);
        }

        var changed = new List<string>();
        foreach (var file in files.Require())
        {
            var original = File.ReadAllText(file);
            if (Format(original, file) is not { } formatted || formatted == original)
            {
                continue;
            }

            changed.Add(file);
            if (!check)
            {
                File.WriteAllText(file, formatted);
            }
        }
        return Result.Success<IReadOnlyList<string>>(changed);
    }

    private static Result<IReadOnlyList<string>> ResolveFiles(string path)
    {
        if (File.Exists(path))
        {
            return Result.Success<IReadOnlyList<string>>([path]);
        }

        if (Directory.Exists(path))
        {
            return Result.Success(ProjectGlobs.Enumerate(path));
        }

        return Result.Failure<IReadOnlyList<string>>(FormatDiagnostics.PathNotFound(path));
    }
}
