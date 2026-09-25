using Microsoft.AspNetCore.Mvc;

namespace Natoshare.Api.Controllers;

// A few small things every controller needs, kept in one place so we do not repeat
// them everywhere.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    // Where the request came from, used for the audit log and for refresh tokens.
    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
