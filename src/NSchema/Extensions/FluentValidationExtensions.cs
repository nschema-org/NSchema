using FluentValidation;
using FluentValidation.Validators;

namespace NSchema.Cli.Extensions;

/// <remarks>
/// Taken from: https://github.com/FluentValidation/FluentValidation/issues/1648
/// </remarks>
internal static class FluentValidationExtensions
{
    extension<T>(IValidator<T> validator)
    {
        /// <summary>
        /// Validates <paramref name="instance"/> and, on failure, throws a single exception whose message joins every validation error.
        /// </summary>
        public void ValidateOrThrow(T instance)
        {
            var result = validator.Validate(instance);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }

    extension<T, TProperty>(IRuleBuilder<T, TProperty?> ruleBuilder)
    {
        public IRuleBuilderOptions<T, TProperty?> SetNonNullableValidator(IValidator<TProperty> validator, params string[] ruleSets)
        {
            var adapter = new NullableChildValidatorAdaptor<T, TProperty>(validator, validator.GetType())
            {
                RuleSets = ruleSets
            };
            return ruleBuilder.SetAsyncValidator(adapter);
        }
    }

    private class NullableChildValidatorAdaptor<T, TProperty>(IValidator<TProperty> validator, Type validatorType)
        : ChildValidatorAdaptor<T, TProperty>(validator, validatorType), IPropertyValidator<T, TProperty?>,
            IAsyncPropertyValidator<T, TProperty?>
    {
        public override bool IsValid(ValidationContext<T> context, TProperty? value)
        {
            return base.IsValid(context, value!);
        }

        public override Task<bool> IsValidAsync(ValidationContext<T> context, TProperty? value, CancellationToken cancellation)
        {
            return base.IsValidAsync(context, value!, cancellation);
        }
    }
}
