using FluentValidation;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.InApp;

// ---------------------------------------------------------------------- notification center (US-3.6.2-01, -07)

public sealed record ListInAppNotificationsQuery(string? Status, int Page = 1, int PageSize = 20)
    : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), IQuery<InAppPage>;

public sealed record InAppPage(PagedResult<InAppNotificationDto> Items, int Unread);

public sealed record MarkNotificationReadCommand(Guid Id) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand;

public sealed record DeleteNotificationCommand(Guid Id) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand;

public sealed record TakeNotificationActionCommand(Guid Id) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand<NotificationActionDto>;

public sealed record NotificationActionDto(string? ActionUrl);

public sealed class ListInAppNotificationsValidator : AbstractValidator<ListInAppNotificationsQuery>
{
    public ListInAppNotificationsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        When(x => !string.IsNullOrEmpty(x.Status), () =>
            RuleFor(x => x.Status!).Must(s => Enum.TryParse<InAppStatus>(s, true, out var v) && v != InAppStatus.Deleted).OverridePropertyName("Status")
                .WithErrorCode("VAL.Status.Invalid"));
    }
}

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}

public sealed class DeleteNotificationValidator : AbstractValidator<DeleteNotificationCommand>
{
    public DeleteNotificationValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}

public sealed class TakeNotificationActionValidator : AbstractValidator<TakeNotificationActionCommand>
{
    public TakeNotificationActionValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}

internal sealed class InAppHandlers :
    IQueryHandler<ListInAppNotificationsQuery, InAppPage>,
    ICommandHandler<MarkNotificationReadCommand, Unit>,
    ICommandHandler<DeleteNotificationCommand, Unit>,
    ICommandHandler<TakeNotificationActionCommand, NotificationActionDto>
{
    private static readonly Error NotFound = Error.NotFound(NotificationErrorCodes.InAppNotFound, "The notification was not found.");

    private readonly IInAppNotificationRepository _repository;
    private readonly INotificationReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public InAppHandlers(IInAppNotificationRepository repository, INotificationReadStore store, ICurrentUser user, TimeProvider clock)
    {
        _repository = repository;
        _store = store;
        _user = user;
        _clock = clock;
    }

    private Actor Me => new(_user.UserId!.Value, _user.ActorType == SharedKernel.Common.Enums.ActorType.Administrator);

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Result<InAppPage>> Handle(ListInAppNotificationsQuery request, CancellationToken ct)
    {
        // Only the caller's own notifications are ever read (INV-01 recipient-scoped).
        var status = string.IsNullOrEmpty(request.Status) ? (InAppStatus?)null : Enum.Parse<InAppStatus>(request.Status, true);
        var page = await _store.ListInAppAsync(Me.Id, status, new PageRequest(request.Page, request.PageSize), ct);
        return new InAppPage(page, await _store.CountUnreadAsync(Me.Id, ct));
    }

    public async Task<Result<Unit>> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await Load(request.Id, ct);
        if (notification is null)
        {
            return NotFound;
        }

        notification.MarkRead(Me, Now);
        return Result.Success();
    }

    public async Task<Result<Unit>> Handle(DeleteNotificationCommand request, CancellationToken ct)
    {
        var notification = await Load(request.Id, ct);
        if (notification is null)
        {
            return NotFound;
        }

        notification.Delete(Me, Now);
        return Result.Success();
    }

    public async Task<Result<NotificationActionDto>> Handle(TakeNotificationActionCommand request, CancellationToken ct)
    {
        var notification = await Load(request.Id, ct);
        return notification is null ? NotFound : new NotificationActionDto(notification.TakeAction(Me, Now));
    }

    /// <summary>A deleted notification is gone (INV-03 to E-INAPPN-NOT-FOUND); someone else's notification is reported by the aggregate as forbidden.</summary>
    private async Task<InAppNotification?> Load(Guid id, CancellationToken ct)
    {
        var notification = await _repository.GetAsync(id, ct);
        return notification is { Status: InAppStatus.Deleted } ? null : notification;
    }
}
