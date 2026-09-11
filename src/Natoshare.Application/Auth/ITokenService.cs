using Natoshare.Domain.Identity;

namespace Natoshare.Application.Auth;

// A freshly made refresh token. RawToken is what we hand to the caller, TokenHash is
// what we save in the database, we never save the raw value.
public record NewRefreshToken(string RawToken, string TokenHash, DateTimeOffset ExpiresAt);

// Builds and checks the tokens that keep a user signed in.
public interface ITokenService
{
    // Builds a signed access token for this user, with their id, email and role
    // inside it so the API can trust who is calling without hitting the database
    // every time.
    string CreateAccessToken(User user, string role);

    NewRefreshToken CreateRefreshToken();

    // Turns a raw token into the same hash we save in the database, so we can look a
    // token up without ever storing the real value.
    string Hash(string rawToken);
}
