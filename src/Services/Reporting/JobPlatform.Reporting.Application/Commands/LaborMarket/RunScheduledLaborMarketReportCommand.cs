using JobPlatform.Reporting.Application.DTOs.LaborMarket;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.LaborMarket;

/// <summary>Scheduled variant (no caller, no access check): generates the report of the previous month when it does not exist yet.</summary>
public sealed record RunScheduledLaborMarketReportCommand : ICommand<LaborMarketReportDto>;
