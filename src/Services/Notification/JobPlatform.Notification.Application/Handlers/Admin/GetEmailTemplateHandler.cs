using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.Queries.Admin;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class GetEmailTemplateHandler : IQueryHandler<GetEmailTemplateQuery, EmailTemplateDto>
{
    private readonly IEmailTemplateRepository _templates;

    public GetEmailTemplateHandler(IEmailTemplateRepository templates) => _templates = templates;

    public async Task<Result<EmailTemplateDto>> Handle(GetEmailTemplateQuery request, CancellationToken ct)
    {
        var template = await _templates.GetCurrentAsync(request.Code, request.Locale, ct);
        return template is null ? Error.NotFound(NotificationErrorCodes.TemplateNotFound, "The template was not found.") : AdminService.ToDto(template);
    }
}
