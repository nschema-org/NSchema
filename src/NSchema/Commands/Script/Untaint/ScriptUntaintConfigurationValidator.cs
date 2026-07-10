using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.Untaint;

internal sealed class ScriptUntaintConfigurationValidator : AbstractValidator<ScriptUntaintConfiguration>
{
    public ScriptUntaintConfigurationValidator()
    {
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to untaint a script. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
