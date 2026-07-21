using FluentValidation;

namespace NSchema.Configuration.State;

internal sealed class FileStateConfigurationValidator : AbstractValidator<FileStateConfiguration>
{
    public FileStateConfigurationValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty()
            .WithMessage("state.file.path is required.");
    }
}
