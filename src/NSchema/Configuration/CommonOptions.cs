using NSchema.Configuration.Binding;

namespace NSchema.Configuration;

/// <summary>
/// Options used by most/all commands.
/// </summary>
internal static class CommonOptions
{
    public static readonly OptionBinding<string> Directory = OptionBinding.Create<string>()
        .FromOption("--directory")
        .Recursive()
        .WithDescription("Project directory to run in. Defaults to the current directory.");

    public static readonly OptionBinding<string?> Environment = OptionBinding.Create<string?>()
        .FromOption("--environment")
        .FromEnvironmentVariable(EnvironmentVariables.Environment)
        .Recursive()
        .WithDescription("Target environment. Layers the matching *.env.<name>.sql overlay files over the base configuration.");

    public static readonly OptionBinding<bool> NoColor = OptionBinding.Create<bool>()
        .FromOption("--no-color")
        .FromEnvironmentVariable(EnvironmentVariables.NoColor)
        .Recursive()
        .WithDescription("Disable colored output.");
}
