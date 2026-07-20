using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

internal sealed class DestroyConfigurationValidator : AbstractValidator<DestroyConfiguration>
{
    public DestroyConfigurationValidator()
    {
        // Destroy generates and executes SQL against a live database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for destroy.");

        // A teardown converges the managed schema recorded in the state towards nothing, so a state store is
        // mandatory — unless --ephemeral-state stands one in for the run.
        RuleFor(x => x.State)
            .NotNull()
            .When(x => !x.EphemeralState)
            .WithMessage("A state store is required for destroy: the managed schema is read from the recorded state. Declare a STATE statement in a configuration (*.env.sql) file, or pass --ephemeral-state.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
