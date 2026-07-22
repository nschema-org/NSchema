using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.Taint;

internal sealed class ScriptTaintConfigurationValidator : AbstractValidator<ScriptTaintConfiguration>
{
    public ScriptTaintConfigurationValidator()
    {
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to taint a script. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State!).SetValidator(new StateConfigurationValidator());
    }
}
