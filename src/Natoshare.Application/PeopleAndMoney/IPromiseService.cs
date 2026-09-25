namespace Natoshare.Application.PeopleAndMoney;

public interface IPromiseService
{
    Task<IReadOnlyList<PromiseDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default);

    Task<PromiseDto> CreateAsync(Guid userId, CreatePromiseRequest request, CancellationToken cancellationToken = default);

    Task<PromiseDto> AddRedemptionAsync(
        Guid userId, Guid promiseId, CreatePromiseRedemptionRequest request, CancellationToken cancellationToken = default);

    Task<PromiseDto> UpdateAsync(Guid userId, Guid promiseId, UpdatePromiseRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid promiseId, CancellationToken cancellationToken = default);
}
