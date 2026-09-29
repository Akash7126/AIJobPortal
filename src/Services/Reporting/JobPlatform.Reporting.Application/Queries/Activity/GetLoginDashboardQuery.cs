using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Activity;

public sealed record GetLoginDashboardQuery : ActivityRequest, IQuery<LoginDashboardDto>;
