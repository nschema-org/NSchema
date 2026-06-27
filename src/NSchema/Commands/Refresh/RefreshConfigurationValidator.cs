using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Refresh;

internal sealed class RefreshConfigurationValidator : AbstractValidator<RefreshConfiguration>
{
    public RefreshConfigurationValidator()
    {
        // Refresh reads the live schema, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for refresh.");

        // Refresh writes the snapshot to the state store, so a store is mandatory.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required for refresh: the live schema is written there.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
