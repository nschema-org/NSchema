using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plan;

internal sealed class PlanConfigurationValidator : AbstractValidator<PlanConfiguration>
{
    public PlanConfigurationValidator()
    {
        // The plan's SQL is rendered against the provider's dialect, so a provider is mandatory.
        RuleFor(x => x.Database)
            .NotNull()
            .WithMessage("A database provider is required for plan: the plan's SQL is rendered against it. Declare a DATABASE statement in a configuration (*.env.sql) file.");

        // A plan always diffs the recorded state against the target, so a state store is mandatory —
        // unless --ephemeral stands one in for the run.
        RuleFor(x => x.State)
            .NotNull()
            .When(x => !x.Ephemeral)
            .WithMessage("A state store is required for plan: the plan diffs the recorded state. Declare a STATE statement in a configuration (*.env.sql) file, or pass --ephemeral.");
        RuleFor(x => x.State!).SetValidator(new StateConfigurationValidator());
    }
}
