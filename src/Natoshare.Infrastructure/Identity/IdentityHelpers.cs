using Microsoft.AspNetCore.Identity;
using Natoshare.Application.Auth;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Identity;

// A couple of small helpers both AuthService and MeService need, kept in one place so
// we do not write the same few lines twice.
internal static class IdentityHelpers
{
    // Right now a user only ever has one role that matters to us. If they are in the
    // Admin role we say Admin, otherwise User.
    public static async Task<string> GetPrimaryRoleAsync(this UserManager<User> userManager, User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Contains("Admin") ? "Admin" : "User";
    }

    // Turns a User row into the safe shape we hand back over the API.
    public static UserSummary ToSummary(this User user, string role) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        role,
        user.CurrencyCode,
        user.CurrencySymbol,
        user.TimeZoneId,
        user.Locale,
        user.TrialEndsAt);
}
