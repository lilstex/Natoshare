namespace Natoshare.Application.PeopleAndMoney;

public interface INetPositionService
{
    Task<NetPositionDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
