using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Notifications;

namespace Natoshare.Api.Controllers;

// What Natoshare has told a user about, and how loud each kind of alert should be
// for them.
[Route("api/v1/notifications")]
[Authorize(Policy = "RequireUser")]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<List<UpdateAlertPreferenceInput>> _preferencesValidator;

    public NotificationsController(
        INotificationService notificationService, ICurrentUser currentUser, IValidator<List<UpdateAlertPreferenceInput>> preferencesValidator)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
        _preferencesValidator = preferencesValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetNotifications(
        bool unreadOnly, string? kind, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _notificationService.ListAsync(_currentUser.UserId, unreadOnly, kind, page, Math.Clamp(pageSize, 1, 100), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await _notificationService.MarkReadAsync(_currentUser.UserId, notificationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllReadAsync(_currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<IReadOnlyList<AlertPreferenceDto>>> GetPreferences(CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetPreferencesAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("preferences")]
    public async Task<ActionResult<IReadOnlyList<AlertPreferenceDto>>> UpdatePreferences(
        List<UpdateAlertPreferenceInput> updates, CancellationToken cancellationToken)
    {
        await _preferencesValidator.ValidateOrThrowAsync(updates, cancellationToken);
        var result = await _notificationService.UpdatePreferencesAsync(_currentUser.UserId, updates, cancellationToken);
        return Ok(result);
    }
}
