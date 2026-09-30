using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.LaborMarket;

/// <summary>Scheduled variant (no caller, no access check): generates the report of the previous month when it does not exist yet.</summary>
public sealed record RunScheduledLaborMarketReportCommand : ICommand<LaborMarketReportDto>;
