namespace Natoshare.Application.Admin;

// Everything the admin app's Users section can do, see docs/04-admin-app.md section
// 2.2. Every action here is audited, and none of them touch anyone's money, only
// their account state (docs/04-admin-app.md's "no money mutation" principle).
public interface IAdminUserService
{
    Task<AdminUserListResult> ListAsync(string? query, string? status, string? plan, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminUserDetailDto> GetDetailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SuspendAsync(Guid adminUserId, Guid userId, SuspendUserRequest request, string? ip, CancellationToken cancellationToken = default);

    Task ReactivateAsync(Guid adminUserId, Guid userId, string? ip, CancellationToken cancellationToken = default);

    Task<AdminResetPasswordResult> ResetPasswordAsync(Guid adminUserId, Guid userId, CancellationToken cancellationToken = default);

    Task AdjustPlanAsync(Guid adminUserId, Guid userId, AdjustPlanRequest request, string? ip, CancellationToken cancellationToken = default);

    Task<AdminExportResult> ExportAsync(Guid adminUserId, Guid userId, string? ip, CancellationToken cancellationToken = default);

    Task HardDeleteAsync(Guid adminUserId, Guid userId, HardDeleteUserRequest request, string? ip, CancellationToken cancellationToken = default);

    Task<RecomputeBalancesResult> RecomputeBalancesAsync(Guid userId, CancellationToken cancellationToken = default);
}
