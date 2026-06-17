using FluentValidation;
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
            .WithMessage($"A database provider is required for import.");

        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.Target).SetValidator(new ImportTargetConfigValidator());
    }
}
