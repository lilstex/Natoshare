namespace Natoshare.Application.Ledger;

public interface IReallocationService
{
    Task<IReadOnlyList<ReallocationDto>> ListAsync(
        Guid userId, DateOnly? from, DateOnly? to, string? reason, CancellationToken cancellationToken = default);

    // The source account has to actually hold the money, unlike an expense this can
    // never push an account into deficit.
    Task<ReallocationDto> CreateAsync(Guid userId, CreateReallocationRequest request, CancellationToken cancellationToken = default);
}
