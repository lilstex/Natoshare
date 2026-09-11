namespace Natoshare.Domain.Common;

// This is the base for every error we throw on purpose, when someone breaks one of
// Natoshare's own rules, for example signing up with an email that is already used.
// The Api project catches these and turns them into a proper JSON error response,
// using the StatusCode below, instead of a raw unhandled exception.
public abstract class DomainException : Exception
{
    public int StatusCode { get; }

    protected DomainException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

// Something the caller is trying to do clashes with what already exists, for example
// signing up with an email that is already registered, or changing a currency that is
// already locked.
public sealed class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message, 409)
    {
    }
}

// We looked for something and it is not there, for example a data export that does not
// exist or does not belong to the caller.
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message, 404)
    {
    }
}

// The caller's credentials, token, or password did not check out.
public sealed class AuthenticationFailedException : DomainException
{
    public AuthenticationFailedException(string message)
        : base(message, 401)
    {
    }
}

// One or more fields on the request did not pass validation. Errors is a map of field
// name to the list of problems with that field, so the frontend can show them next to
// the right input.
public sealed class ValidationFailedException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more fields are not valid.", 422)
    {
        Errors = errors;
    }
}
