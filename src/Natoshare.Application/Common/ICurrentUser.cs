namespace Natoshare.Application.Common;

// Tells us who is making the current request, read from their access token.
// Application services use this instead of touching HttpContext directly, so they do
// not need to know anything about how the request actually arrived.
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string? Email { get; }
    string Role { get; }
}
