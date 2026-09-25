namespace Natoshare.Application.Admin;

// Lets an admin tune what Free and Pro can each do without a deploy, see
// docs/04-admin-app.md section 2.3. There are only ever two rows here (one per
// PlanTier), seeded once by ReferenceDataSeeder.
public interface IAdminPlanService
{
    Task<IReadOnlyList<AdminPlanConfigDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdminPlanConfigDto> UpdateAsync(string plan, PatchPlanConfigRequest request, Guid adminUserId, CancellationToken cancellationToken = default);
}
