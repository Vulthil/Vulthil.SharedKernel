using FluentValidation;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Provides FluentValidation integration helpers for <see cref="Error"/>.
/// </summary>
public static class FluentValidationExtensions
{
    /// <summary>
    /// Attaches the code and description from an <see cref="Error"/> to the validation rule.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type being validated.</typeparam>
    /// <param name="rule">The rule builder to configure.</param>
    /// <param name="error">The error whose code and description should be used.</param>
    /// <returns>The configured rule builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, Error error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "The error is required");
        }

        return rule.WithErrorCode(error.Code).WithMessage(error.Description);
    }
}
