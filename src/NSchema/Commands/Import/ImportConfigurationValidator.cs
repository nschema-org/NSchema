using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Import;
using NSchema.Configuration.Provider;

namespace NSchema.Commands.Import;

internal sealed class ImportConfigurationValidator : AbstractValidator<ImportConfiguration>
{
    public ImportConfigurationValidator()
    {
        // Import reads from a live database, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for import. Configure provider.postgres in nschema.json, or set {EnvironmentVariables.PostgresConnectionString}.");

        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.Target).SetValidator(new ImportTargetConfigValidator());
    }
}
