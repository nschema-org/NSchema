using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Drift;

internal sealed class DriftConfigurationValidator : AbstractValidator<DriftConfiguration>
{
    public DriftConfigurationValidator()
    {
        // Drift reads the live schema to compare against the recorded state, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for drift.");

        // Drift compares the live schema against the recorded state, so a state store is mandatory.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required for drift: the live schema is compared against the recorded state there.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
