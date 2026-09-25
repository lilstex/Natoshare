using FluentValidation;
using Natoshare.Domain.Common;

namespace Natoshare.Api.Validation;

// A small helper so every controller checks a request the same way: run the
// FluentValidation validator, and if it fails, throw our own ValidationFailedException
// so the DomainExceptionMiddleware can turn it into a proper 422 response.
public static class ValidatorExtensions
{
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        throw new ValidationFailedException(errors);
    }
}
