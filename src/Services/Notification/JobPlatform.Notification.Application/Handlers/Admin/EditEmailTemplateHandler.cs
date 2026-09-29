using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class EditEmailTemplateHandler : ICommandHandler<EditEmailTemplateCommand, EmailTemplateDto>
{
    private readonly IEmailTemplateRepository _templates;
    private readonly AdminService _adminService;

    public EditEmailTemplateHandler(IEmailTemplateRepository templates, AdminService adminService)
    {
        _templates = templates;
        _adminService = adminService;
    }

    public async Task<Result<EmailTemplateDto>> Handle(EditEmailTemplateCommand request, CancellationToken ct)
    {
        var current = await _templates.GetCurrentAsync(request.Code, request.Locale, ct);
        var next = current is null
            ? EmailTemplate.Create(request.Code, request.Locale, request.Subject, request.Body, request.Placeholders, _adminService.Now)
            : current.NewVersion(_adminService.Me, request.Subject, request.Body, request.Placeholders, _adminService.Now);
        _templates.Add(next);
        return AdminService.ToDto(next);
    }
}
