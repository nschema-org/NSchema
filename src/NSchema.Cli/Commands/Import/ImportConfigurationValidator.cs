using FluentValidation;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;

namespace NSchema.Cli.Commands.Import;

internal sealed class ImportConfigurationValidator : AbstractValidator<ImportConfiguration>
{
    public ImportConfigurationValidator()
    {
        // Import reads from a live database, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for import. Configure one in nschema.json, via --provider/--connection-string, or {EnvironmentVariables.ConnectionString}.");

        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.ImportTarget).SetValidator(new ImportTargetConfigValidator());
    }
}
