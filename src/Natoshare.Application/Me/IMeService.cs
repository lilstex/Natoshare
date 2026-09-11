namespace Natoshare.Application.Me;

// Everything a logged in user can do to their own profile and account.
public interface IMeService
{
    Task<GetMeResult> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<GetMeResult> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken = default);

    Task<GetMeResult> UpdateCurrencyAsync(Guid userId, UpdateCurrencyRequest request, CancellationToken cancellationToken = default);

    // Starts building a copy of everything Natoshare has for this user. Returns the id
    // of the export so the caller can check on it later.
    Task<Guid> RequestExportAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ExportStatusResult> GetExportStatusAsync(Guid userId, Guid exportId, CancellationToken cancellationToken = default);

    // Moves the account to "pending deletion". A background job removes it for good
    // after a grace period, that job gets built in a later phase.
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, string? ip, CancellationToken cancellationToken = default);
}
