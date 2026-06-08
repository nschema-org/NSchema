using NSchema.Configuration.Binding;

namespace NSchema.Configuration;

/// <summary>
/// The cross-cutting options that steer the harness rather than a command's configuration: the config-file
/// location (read by <see cref="ConfigurationFactory"/>) and color output (read at the root by the console
/// setup). Options that bind into a command's configuration are owned by that command's own option set.
/// </summary>
internal static class CommonOptions
{
    public static readonly OptionBinding<string> Config = OptionBinding.Create<string>()
        .FromOption("--config")
        .WithDescription("Path to the NSchema config file. Defaults to ./nschema.json if present.");

    public static readonly OptionBinding<bool> NoColor = OptionBinding.Create<bool>()
        .FromOption("--no-color")
        .FromEnvironmentVariable(EnvironmentVariables.NoColor)
        .Recursive()
        .WithDescription("Disable colored output.");
}
