using FluentValidation;

namespace NSchema.Configuration.State;

internal sealed class FileStateConfigValidator : AbstractValidator<FileStateConfig>
{
    public FileStateConfigValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty()
            .WithMessage("state.file.path is required.");
    }
}
