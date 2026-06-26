using FluentValidation;

namespace NSchema.Configuration.State;

internal sealed class StateConfigValidator : AbstractValidator<StateConfig>
{
    public StateConfigValidator()
    {
        RuleFor(x => x.ConfiguredSectionCount)
            .LessThanOrEqualTo(1)
            .WithMessage("More than one state store is configured; specify exactly one.");

        RuleFor(x => x.File)
            .SetNonNullableValidator(new FileStateConfigValidator());
    }
}
