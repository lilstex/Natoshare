namespace Natoshare.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    string Severity,
    string? RelatedEntityType,
    string? RelatedEntityId,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public record AlertPreferenceDto(string Kind, bool Enabled, decimal? ThresholdPercent, int? LeadDays);

public record UpdateAlertPreferenceInput(string Kind, bool Enabled, decimal? ThresholdPercent, int? LeadDays);
