using FluentValidation;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Doctor;

internal sealed class DoctorConfigurationValidator : AbstractValidator<DoctorConfiguration>
{
    public DoctorConfigurationValidator()
    {
        // Doctor needs something to check: a project that declares neither a provider nor a state store has no
        // infrastructure to probe, so running it there is a usage error rather than a vacuous pass.
        RuleFor(x => x.Provider.ConfiguredSectionCount + x.State.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Nothing to check: declare a database provider and/or a state store in your project configuration.");

        // The provider and state store are each optional, but when present they must be well-formed.
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
