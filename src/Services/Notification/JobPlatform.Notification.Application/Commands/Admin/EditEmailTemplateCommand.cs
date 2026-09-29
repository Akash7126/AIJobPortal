using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Admin;

public sealed record EditEmailTemplateCommand(string Code, string Locale, string Subject, string Body, IReadOnlyDictionary<string, string> Placeholders)
    : AdminRequest(NotificationErrorCodes.EmailForbidden), ICommand<EmailTemplateDto>;
