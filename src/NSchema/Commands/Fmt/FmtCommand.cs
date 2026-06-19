using System.CommandLine;
using NSchema.Schema.Ddl;

namespace NSchema.Commands.Fmt;

internal static class FmtCommand
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

    public static Command Create()
    {
        var command = new Command("fmt", "Reformat .sql DDL files to a canonical layout (in place, or check with --check).");
        command.Arguments.Add(PathArgument);
        command.Options.Add(CheckOption);
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
        foreach (var file in changed)
        {
            Console.Out.WriteLine(file);
        }
        return check && changed.Count > 0 ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }

    private static int FormatStdin(bool check)
    {
        var input = Console.In.ReadToEnd();
        var formatted = DdlFormatter.Instance.Format(input);
        if (check)
        {
            return formatted == input ? ExitCodes.NoChanges : ExitCodes.HasChanges;
        }
        Console.Out.Write(formatted);
        return ExitCodes.NoChanges;
    }

    /// <summary>
    /// Formats every <c>.sql</c> file under <paramref name="path"/> (a single file or a directory tree). Returns the
    /// files whose content changed; when <paramref name="check"/> is <see langword="false"/> those files are rewritten.
    /// </summary>
    internal static IReadOnlyList<string> FormatPath(string path, bool check)
    {
        var changed = new List<string>();
        foreach (var file in ResolveFiles(path))
        {
            var original = File.ReadAllText(file);
            var formatted = DdlFormatter.Instance.Format(original);
            if (formatted == original)
            {
                continue;
            }

            changed.Add(file);
            if (!check)
            {
                File.WriteAllText(file, formatted);
            }
        }
        return changed;
    }

    private static IEnumerable<string> ResolveFiles(string path)
    {
        if (File.Exists(path))
        {
            return [path];
        }
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.sql", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.Ordinal);
        }
        throw new FileNotFoundException($"No such file or directory: '{path}'.");
    }
}
