namespace Natoshare.Application.Admin;

// The key/value settings an admin can tune without a deploy, see docs/04-admin-app.md
// section 2.6, for example how many days a new signup's trial lasts. Everyday app
// code (like AuthService at signup) reads these through GetTrialDaysAsync instead of
// the admin CRUD methods below, and falls back to AppDefaults when nobody has set a
// row yet, so a fresh database still behaves sensibly before an admin ever visits
// the settings screen.
public interface ISystemSettingsService
{
    Task<IReadOnlyList<SystemSettingDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<SystemSettingDto> SetAsync(string key, PatchSettingRequest request, Guid adminUserId, CancellationToken cancellationToken = default);

    Task<int> GetTrialDaysAsync(CancellationToken cancellationToken = default);
}
