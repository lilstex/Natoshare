namespace Natoshare.Application.PeopleAndMoney;

public interface IObligationsService
{
    // A calendar of upcoming dates the user should keep an eye on, from today out to
    // `days` days ahead.
    Task<IReadOnlyList<ObligationItemDto>> ListAsync(Guid userId, int days, CancellationToken cancellationToken = default);
}
