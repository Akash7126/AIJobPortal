using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.LaborMarket;

/// <summary>Idempotent: an existing report for the period is returned, never duplicated. Period null = the previous calendar month.</summary>
public sealed record GenerateLaborMarketReportCommand(string? Period) : EmploymentCommandRequest, ICommand<LaborMarketReportDto>, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ReportingErrorCodes.EmploymentInvalidField;
}
