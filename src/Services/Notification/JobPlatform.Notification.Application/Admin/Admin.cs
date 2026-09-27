using FluentValidation;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Admin;

// ---------------------------------------------------------------------- administrator configuration (US-3.6.1-02, 3.6.2-06, 3.6.3-03, 3.6.3-04)

public sealed record GetEmailTemplateQuery(string Code, string Locale) : AdminRequest(NotificationErrorCodes.EmailForbidden), IQuery<EmailTemplateDto>;

public sealed record EditEmailTemplateCommand(string Code, string Locale, string Subject, string Body, IReadOnlyDictionary<string, string> Placeholders)
    : AdminRequest(NotificationErrorCodes.EmailForbidden), ICommand<EmailTemplateDto>;

public sealed record ListNotificationTypesQuery : AdminRequest, IQuery<IReadOnlyList<NotificationTypeDto>>
{
    public ListNotificationTypesQuery() : base(NotificationErrorCodes.TypeForbidden)
    {
    }
}

public sealed record DefineNotificationTypeCommand(string Code, string Icon, string Colour, string TextAlternative, bool IsMandatory)
    : AdminRequest(NotificationErrorCodes.TypeForbidden), ICommand<NotificationTypeDto>;

public sealed record GetEssentialSmsCategoriesQuery : AdminRequest, IQuery<IReadOnlyCollection<string>>
{
    public GetEssentialSmsCategoriesQuery() : base(NotificationErrorCodes.SmsForbidden)
    {
    }
}

public sealed record ConfigureEssentialSmsCategoriesCommand(IReadOnlyCollection<string> Categories)
    : AdminRequest(NotificationErrorCodes.SmsForbidden), ICommand<IReadOnlyCollection<string>>;

public sealed record ListSmsDeliveryStatusQuery(string? Status, int Page = 1, int PageSize = 20)
    : AdminRequest(NotificationErrorCodes.SmsForbidden), IQuery<PagedResult<SmsDeliveryDto>>;

public sealed class EditEmailTemplateValidator : AbstractValidator<EditEmailTemplateCommand>
{
    public EditEmailTemplateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Code.Required");
        RuleFor(x => x.Locale).Must(l => l is "ar" or "en").WithErrorCode("VAL.Locale.Invalid");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Subject.Invalid");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(100_000).WithErrorCode("VAL.Body.Invalid");
        RuleFor(x => x.Placeholders).NotNull().WithErrorCode("VAL.Placeholders.Required");
    }
}

public sealed class DefineNotificationTypeValidator : AbstractValidator<DefineNotificationTypeCommand>
{
    public DefineNotificationTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Code.Required");
        RuleFor(x => x.Icon).Must(i => NotificationType.AllowedIcons.Contains(i)).WithErrorCode("VAL.Icon.NotAllowed");
        RuleFor(x => x.Colour).Must(c => Contrast.OnWhite(c) >= 4.5).WithErrorCode("VAL.Colour.LowContrast");
        RuleFor(x => x.TextAlternative).NotEmpty().MaximumLength(200).WithErrorCode("VAL.TextAlternative.Required");
    }
}

public sealed class ConfigureEssentialSmsCategoriesValidator : AbstractValidator<ConfigureEssentialSmsCategoriesCommand>
{
    public ConfigureEssentialSmsCategoriesValidator()
    {
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleForEach(x => x.Categories).Must(Categories.IsKnown).WithErrorCode("VAL.Category.Unknown");
        RuleFor(x => x.Categories).Must(c => Categories.DefaultEssentialSms.All(e => c.Contains(e, StringComparer.OrdinalIgnoreCase)))
            .WithErrorCode("VAL.Categories.OtpAndResetRequired");
    }
}

public sealed class ListSmsDeliveryStatusValidator : AbstractValidator<ListSmsDeliveryStatusQuery>
{
    public ListSmsDeliveryStatusValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        When(x => !string.IsNullOrEmpty(x.Status), () =>
            RuleFor(x => x.Status!).Must(s => Enum.TryParse<DeliveryStatus>(s, true, out _)).OverridePropertyName("Status").WithErrorCode("VAL.Status.Invalid"));
    }
}

internal sealed class AdminHandlers :
    IQueryHandler<GetEmailTemplateQuery, EmailTemplateDto>,
    ICommandHandler<EditEmailTemplateCommand, EmailTemplateDto>,
    IQueryHandler<ListNotificationTypesQuery, IReadOnlyList<NotificationTypeDto>>,
    ICommandHandler<DefineNotificationTypeCommand, NotificationTypeDto>,
    IQueryHandler<GetEssentialSmsCategoriesQuery, IReadOnlyCollection<string>>,
    ICommandHandler<ConfigureEssentialSmsCategoriesCommand, IReadOnlyCollection<string>>,
    IQueryHandler<ListSmsDeliveryStatusQuery, PagedResult<SmsDeliveryDto>>
{
    private readonly IEmailTemplateRepository _templates;
    private readonly INotificationTypeRepository _types;
    private readonly ISmsPolicyRepository _policy;
    private readonly INotificationReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AdminHandlers(IEmailTemplateRepository templates, INotificationTypeRepository types, ISmsPolicyRepository policy, INotificationReadStore store, ICurrentUser user,
        TimeProvider clock)
    {
        _templates = templates;
        _types = types;
        _policy = policy;
        _store = store;
        _user = user;
        _clock = clock;
    }

    private Actor Me => new(_user.UserId!.Value, _user.ActorType == SharedKernel.Common.Enums.ActorType.Administrator);

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private static EmailTemplateDto ToDto(EmailTemplate t) => new(t.Code, t.Locale, t.Version, t.Subject, t.Body, t.Placeholders);

    public async Task<Result<EmailTemplateDto>> Handle(GetEmailTemplateQuery request, CancellationToken ct)
    {
        var template = await _templates.GetCurrentAsync(request.Code, request.Locale, ct);
        return template is null ? Error.NotFound(NotificationErrorCodes.TemplateNotFound, "The template was not found.") : ToDto(template);
    }

    public async Task<Result<EmailTemplateDto>> Handle(EditEmailTemplateCommand request, CancellationToken ct)
    {
        var current = await _templates.GetCurrentAsync(request.Code, request.Locale, ct);
        var next = current is null
            ? EmailTemplate.Create(request.Code, request.Locale, request.Subject, request.Body, request.Placeholders, Now)
            : current.NewVersion(Me, request.Subject, request.Body, request.Placeholders, Now);
        _templates.Add(next);
        return ToDto(next);
    }

    public async Task<Result<IReadOnlyList<NotificationTypeDto>>> Handle(ListNotificationTypesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<NotificationTypeDto>>((await _types.ListAsync(ct)).Select(ToDto).ToList());

    public async Task<Result<NotificationTypeDto>> Handle(DefineNotificationTypeCommand request, CancellationToken ct)
    {
        var existing = await _types.GetAsync(request.Code, ct);
        if (existing is null)
        {
            existing = NotificationType.Define(Me, request.Code, request.Icon, request.Colour, request.TextAlternative, request.IsMandatory);
            _types.Add(existing);
        }
        else
        {
            existing.Redefine(Me, request.Icon, request.Colour, request.TextAlternative, request.IsMandatory);
        }

        return ToDto(existing);
    }

    private static NotificationTypeDto ToDto(NotificationType t) => new(t.Id, t.Icon, t.Colour, t.TextAlternative, t.IsMandatory);

    public async Task<Result<IReadOnlyCollection<string>>> Handle(GetEssentialSmsCategoriesQuery request, CancellationToken ct) =>
        Result.Success((await _policy.GetCurrentAsync(ct)).EssentialCategories);

    public async Task<Result<IReadOnlyCollection<string>>> Handle(ConfigureEssentialSmsCategoriesCommand request, CancellationToken ct)
    {
        var next = (await _policy.GetCurrentAsync(ct)).NewVersion(Me, request.Categories, Now);
        _policy.Add(next);
        return Result.Success(next.EssentialCategories);
    }

    public async Task<Result<PagedResult<SmsDeliveryDto>>> Handle(ListSmsDeliveryStatusQuery request, CancellationToken ct) =>
        await _store.ListSmsDeliveriesAsync(string.IsNullOrEmpty(request.Status) ? null : Enum.Parse<DeliveryStatus>(request.Status, true),
            new PageRequest(request.Page, request.PageSize), ct);
}
