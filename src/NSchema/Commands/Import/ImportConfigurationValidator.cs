using FluentValidation;

namespace NSchema.Commands.Import;

internal sealed class ImportConfigurationValidator : AbstractValidator<ImportConfiguration>
{
    public ImportConfigurationValidator()
    {
        // Import reads from a live database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for import.");
    }
}
