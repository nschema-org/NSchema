using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.New;

/// <summary>
/// Configuration for the new command.
/// </summary>
internal sealed class NewConfiguration : IBindable
{
    /// <summary>
    /// Whether to scaffold the project even if the directory isn't empty.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// The database provider to scaffold configuration and a sample schema for.
    /// </summary>
    public DatabaseKind Database { get; set; } = DatabaseKind.Postgres;

    /// <summary>
    /// The state backend to scaffold configuration for.
    /// </summary>
    public StateKind State { get; set; } = StateKind.File;

    /// <summary>
    /// Whether to skip the automatic <c>init</c> that resolves and locks the scaffolded plugins.
    /// </summary>
    public bool NoInit { get; set; }

    /// <summary>
    /// Answers supplied up front for the plugins' scaffolding questions, keyed by prompt.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Answers { get; private set; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        NewOptions.Force.Bind(cli, f => Force = f);
        NewOptions.Database.Bind(cli, p => Database = p);
        NewOptions.State.Bind(cli, b => State = b);
        NewOptions.NoInit.Bind(cli, n => NoInit = n);
        NewOptions.Set.Bind(cli, values => Answers = ParseAnswers(values));
    }

    // Each --set is one key=value pair; the value may itself contain '=' (a connection string usually does).
    private static Dictionary<string, string?> ParseAnswers(IEnumerable<string> values)
    {
        var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var separator = value.IndexOf('=');
            if (separator > 0)
            {
                answers[value[..separator].Trim()] = value[(separator + 1)..];
            }
        }

        return answers;
    }
}
