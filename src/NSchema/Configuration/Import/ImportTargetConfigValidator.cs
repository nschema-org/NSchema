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
                .WithMessage("An output file is required when partition is None.");
            RuleFor(x => x.OutputDirectory)
                .Empty()
                .WithMessage("Output directory is not used when partition is None.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.OutputDirectory)
                .NotEmpty()
                .WithMessage("Output directory is required when partition is Schema or Table.");
            RuleFor(x => x.OutputFile)
                .Empty()
                .WithMessage("Output file is not used when partition is Schema or Table; pass output directory instead.");
        });
    }
}
