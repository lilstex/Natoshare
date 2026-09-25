namespace Natoshare.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid userId, bool unreadOnly, string? kind, int page, int pageSize, CancellationToken cancellationToken = default);

    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertPreferenceDto>> UpdatePreferencesAsync(
        Guid userId, List<UpdateAlertPreferenceInput> updates, CancellationToken cancellationToken = default);
}
