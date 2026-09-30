using JobPlatform.Reporting.Application.DTOs.Schedules;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Schedules;

// US-3.5.4-04 report schedules: configuration plus the worker that runs due schedules and asks BC-13 to e-mail the report (ReportDistributionRequested).

public sealed record ListSchedulesQuery : CustomRequest, IQuery<IReadOnlyList<ScheduleDto>>;
