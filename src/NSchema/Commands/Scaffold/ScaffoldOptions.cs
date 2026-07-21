using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Scaffold;

internal static class ScaffoldOptions
{
    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force", "-f")
        .WithDescription("Scaffold even in a non-empty directory, overwriting any files.");

    public static readonly OptionBinding<DatabaseKind> Database = OptionBinding.Create<DatabaseKind>()
        .FromOption("--database")
        .WithDescription("Database provider to scaffold configuration for: postgres (default), sqlite, or sqlserver.");

    public static readonly OptionBinding<StateKind> State = OptionBinding.Create<StateKind>()
        .FromOption("--state")
        .WithDescription("State backend to scaffold configuration for: file (default) or s3.");

    public static readonly OptionBinding<bool> NoInit = OptionBinding.Create<bool>()
        .FromOption("--no-init")
        .WithDescription("Skip resolving and locking the scaffolded plugins; run 'nschema init' yourself later.");

    public static IEnumerable<Option> All => [Force.Option, Database.Option, State.Option, NoInit.Option];
}
