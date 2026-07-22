using FluentValidation;

namespace NSchema.Configuration.State;

internal sealed class StateConfigurationValidator : AbstractValidator<StateConfiguration>
{
    public StateConfigurationValidator()
    {
        // The built-in file store and a backend plugin are mutually exclusive — at most one BACKEND block.
        RuleFor(x => x)
            .Must(state => state.File is null || state.Plugin is null)
            .WithMessage("More than one state store is configured; specify exactly one.");

        RuleFor(x => x.File)
            .SetNonNullableValidator(new FileStateConfigurationValidator());
    }
}
