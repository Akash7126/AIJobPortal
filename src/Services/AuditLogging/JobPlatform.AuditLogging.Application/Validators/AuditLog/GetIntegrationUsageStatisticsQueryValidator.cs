using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

/// <summary>Only presence is checked here; the range rule (end not before start, at most 12 months) is a domain rule reported as E-TPJPRI-INVALID-FIELD.</summary>
public sealed class GetIntegrationUsageStatisticsQueryValidator : AbstractValidator<GetIntegrationUsageStatisticsQuery>
{
    public GetIntegrationUsageStatisticsQueryValidator()
    {
        RuleFor(x => x.From).NotEqual(default(DateOnly)).WithErrorCode("VAL.From.Required");
        RuleFor(x => x.To).NotEqual(default(DateOnly)).WithErrorCode("VAL.To.Required");
    }
}
