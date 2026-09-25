using Hangfire.Dashboard;

namespace Natoshare.Api.Auth;

// Lets only a logged in Admin see the Hangfire dashboard, in every environment, not
// just development (docs/04-admin-app.md section 2.5 wants jobs monitoring gated to
// admins, not wide open like it was before Phase 10). This runs after
// UseAuthentication/UseAuthorization, so httpContext.User is already populated from
// whatever token the request carried, including the ?accessToken= query string trick
// wired up in AuthenticationServiceCollectionExtensions for this exact route.
//
// Accepts either a normal Admin session token, or the narrow, short-lived
// "purpose": "hangfire-dashboard" token from ITokenService.CreateHangfireDashboardToken
// (which the admin app actually links to, see AdminMonitoringController). The narrow
// token proves nothing about a role, only that whoever called
// /admin/hangfire-token was already an Admin at that moment, which is enough: the
// whole point of minting it is that a full-privilege Admin bearer token never has to
// sit in a URL (browser history, proxy logs, same-origin Referer headers) just to
// open a link.
public class AdminOnlyDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return httpContext.User.IsInRole("Admin") || httpContext.User.HasClaim("purpose", "hangfire-dashboard");
    }
}
