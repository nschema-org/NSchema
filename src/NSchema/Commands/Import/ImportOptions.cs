using NSchema.Configuration.Binding;

namespace NSchema.Commands.Import;

internal static class ImportOptions
{
    public static readonly OptionBinding<string[]> Tables = OptionBinding.Create<string[]>()
        .FromOption("--tables")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database tables. May be specified multiple times.");
}
