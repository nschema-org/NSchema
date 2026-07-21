using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Doctor;

internal sealed class DoctorConfigurationValidator : AbstractValidator<DoctorConfiguration>
{
    public DoctorConfigurationValidator()
    {
        // Doctor needs something to check: a project that declares neither a provider nor a state store has no
        // infrastructure to probe, so running it there is a usage error rather than a vacuous pass.
        RuleFor(x => x)
            .Must(c => c.Database is not null || c.State is not null)
            .WithMessage("Nothing to check: declare a database provider and/or a state store in your project configuration.");

        // The state store is optional, but when present it must be well-formed.
        RuleFor(x => x.State!).SetValidator(new StateConfigurationValidator());
    }
}
