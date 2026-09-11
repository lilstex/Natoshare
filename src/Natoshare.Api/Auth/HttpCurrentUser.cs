using System.Security.Claims;
using Natoshare.Application.Common;

namespace Natoshare.Api.Auth;

// Reads who is making the current request from their access token claims, so the rest
// of the app can just ask ICurrentUser instead of digging through HttpContext itself.
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Principal?.FindFirstValue("sub");

            return value is not null && Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? "User";
}
