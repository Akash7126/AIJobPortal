using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportRuns;

/// <summary>Sets the categories of a role (created when missing). Takes effect on the next request of every holder of the role.</summary>
public sealed record ConfigureReportAccessCommand(string Role, IReadOnlyList<string> Categories) : AccessAdminRequest, ICommand<AccessRuleDto>;
