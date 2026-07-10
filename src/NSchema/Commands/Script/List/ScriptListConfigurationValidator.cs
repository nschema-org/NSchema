using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.List;

internal sealed class ScriptListConfigurationValidator : AbstractValidator<ScriptListConfiguration>
{
    public ScriptListConfigurationValidator()
    {
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to list recorded script executions. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
