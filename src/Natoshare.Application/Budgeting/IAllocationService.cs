namespace Natoshare.Application.Budgeting;

// Everything to do with "how much do I earn, and how is it split".
public interface IAllocationService
{
    Task<CurrentAllocationResult?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationVersionResult>> GetVersionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AllocationVersionResult> GetVersionAsync(Guid userId, Guid versionId, CancellationToken cancellationToken = default);

    Task<AllocationVersionResult> CreateVersionAsync(Guid userId, CreateAllocationVersionRequest request, CancellationToken cancellationToken = default);

    // A dry run: works out what each category would get, without saving anything.
    AllocationPreviewResult Preview(AllocationPreviewRequest request);
}
