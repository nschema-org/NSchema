using FluentValidation;

namespace NSchema.Cli.Configuration.Import;

internal sealed class ImportTargetConfigValidator : AbstractValidator<ImportTargetConfig>
{
    public ImportTargetConfigValidator()
    {
        RuleFor(x => x.OutputPath)
            .NotEmpty()
            .WithMessage("An output path is required for import. Set --output or importTarget.outputPath in nschema.json.");
    }
}
