using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Apply;

internal sealed class ApplyConfigurationValidator : AbstractValidator<ApplyConfiguration>
{
    public ApplyConfigurationValidator()
    {
        // Apply writes to a live database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for apply.");

        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
