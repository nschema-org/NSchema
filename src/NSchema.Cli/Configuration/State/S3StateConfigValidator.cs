using FluentValidation;

namespace NSchema.Cli.Configuration.State;

internal sealed class S3StateConfigValidator : AbstractValidator<S3StateConfig>
{
    public S3StateConfigValidator()
    {
        RuleFor(x => x.Bucket)
            .NotEmpty()
            .WithMessage("state.s3.bucket is required.");

        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("state.s3.key is required.");
    }
}
