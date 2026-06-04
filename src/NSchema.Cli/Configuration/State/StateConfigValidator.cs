using FluentValidation;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Configuration.State;

internal sealed class StateConfigValidator : AbstractValidator<StateConfig>
{
    public StateConfigValidator()
    {
        RuleFor(x => x)
            .Must(HaveOnlyOneConfiguration)
            .WithMessage("More than one state store is configured; specify exactly one.");

        RuleFor(x => x.File)
            .SetNonNullableValidator(new FileStateConfigValidator())
            .When(x => x != null);

        RuleFor(x => x.S3)
            .SetNonNullableValidator(new S3StateConfigValidator())
            .When(x => x != null);
    }

    private static bool HaveOnlyOneConfiguration(StateConfig config) => new []
    {
        config.File is not null,
        config.S3 is not null,
    }.Count(x => x) <= 1;
}
