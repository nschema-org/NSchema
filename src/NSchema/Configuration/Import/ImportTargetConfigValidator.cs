using FluentValidation;
using NSchema.Operations.Import;

namespace NSchema.Configuration.Import;

internal sealed class ImportTargetConfigValidator : AbstractValidator<ImportTargetConfig>
{
    public ImportTargetConfigValidator()
    {
        // The partition mode decides whether the import writes a single file or a directory of files, so it dictates
        // which output option is required — and makes the other one a mistake worth flagging rather than silently ignoring.
        When(x => x.Partition == ImportPartitionMode.None, () =>
        {
            RuleFor(x => x.OutputFile)
                .NotEmpty()
                .WithMessage("An output file is required when --partition is None (the schema is written as a single file). Set --output-file.");
            RuleFor(x => x.OutputDirectory)
                .Empty()
                .WithMessage("--output-dir is not used when --partition is None; pass --output-file instead.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.OutputDirectory)
                .NotEmpty()
                .WithMessage("An output directory is required when --partition is Schema or Table (one file is written per namespace/table). Set --output-dir.");
            RuleFor(x => x.OutputFile)
                .Empty()
                .WithMessage("--output-file is not used when --partition is Schema or Table; pass --output-dir instead.");
        });
    }
}
