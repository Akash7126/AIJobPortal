using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Queries.Admin;

public sealed record GetEmailTemplateQuery(string Code, string Locale) : AdminRequest(NotificationErrorCodes.EmailForbidden), IQuery<EmailTemplateDto>;
