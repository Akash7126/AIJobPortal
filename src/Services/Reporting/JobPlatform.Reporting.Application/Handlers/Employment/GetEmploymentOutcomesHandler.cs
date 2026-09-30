using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetEmploymentOutcomesHandler : IQueryHandler<GetEmploymentOutcomesQuery, EmploymentOutcomesDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetEmploymentOutcomesHandler(IAnalyticsQueryService analytics, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<EmploymentOutcomesDto>> Handle(GetEmploymentOutcomesQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetEmploymentOutcomesQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var rows = await _analytics.OutcomesAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        // RP.Outcome.EXCLUDE_NO_FOLLOWUP: records without follow-up data are excluded, never filled with placeholders.
        var withFollowUp = rows.Where(r => r.FollowUps > 0).OrderBy(r => r.FirstShortlistedAtUtc).ToList();
        var items = withFollowUp.Select(r => new OutcomeItemDto(r.JobPostingId, r.Shortlisted, r.FollowUps, r.FitCount == 0 ? null : Math.Round(r.FitSum / r.FitCount, 4))).ToList();
        return new EmploymentOutcomesDto(range.From, range.To, items, rows.Count - withFollowUp.Count);
    }
}
