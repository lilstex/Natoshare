namespace Natoshare.Application.Admin;

// The admin app's on/off switches, see docs/04-admin-app.md section 2.6.
public interface IAdminFeatureFlagService
{
    Task<IReadOnlyList<AdminFeatureFlagDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdminFeatureFlagDto> UpdateAsync(string key, PatchFeatureFlagRequest request, Guid adminUserId, CancellationToken cancellationToken = default);
}
