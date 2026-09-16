using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Notifications;
using Natoshare.Domain.Common;
using Natoshare.Domain.Notifications;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Notifications;

// The notification centre: reading what Natoshare has told a user, marking things
// read, and letting them turn one kind of alert up, down, or off.
public class NotificationService : INotificationService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public NotificationService(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid userId, bool unreadOnly, string? kind, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (kind is not null)
        {
            query = query.Where(n => n.Kind.ToString() == kind);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return notifications.Select(ToDto).ToList();
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that notification.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = _clock.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        var now = _clock.UtcNow;

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await _dbContext.AlertPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        return preferences.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AlertPreferenceDto>> UpdatePreferencesAsync(
        Guid userId, List<UpdateAlertPreferenceInput> updates, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AlertPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);

        foreach (var update in updates)
        {
            var kind = Enum.Parse<NotificationKind>(update.Kind);
            var preference = existing.FirstOrDefault(p => p.Kind == kind);

            if (preference is null)
            {
                preference = new AlertPreference { Id = Guid.CreateVersion7(), UserId = userId, Kind = kind };
                _dbContext.AlertPreferences.Add(preference);
                existing.Add(preference);
            }

            preference.Enabled = update.Enabled;
            preference.ThresholdPercent = update.ThresholdPercent;
            preference.LeadDays = update.LeadDays;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing.Select(ToDto).ToList();
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id, n.Kind.ToString(), n.Title, n.Body, n.Severity.ToString(), n.RelatedEntityType, n.RelatedEntityId, n.IsRead, n.ReadAt, n.CreatedAt);

    private static AlertPreferenceDto ToDto(AlertPreference p) => new(p.Kind.ToString(), p.Enabled, p.ThresholdPercent, p.LeadDays);
}
