using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetEmploymentStatisticsHandler : IQueryHandler<GetEmploymentStatisticsQuery, EmploymentStatisticsDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetEmploymentStatisticsHandler(IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<EmploymentStatisticsDto>> Handle(GetEmploymentStatisticsQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetEmploymentStatisticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var registrations = await _analytics.RegistrationsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var sample = postings.Count + registrations.Count;
        if (_policy.IsInsufficient(sample))
        {
            return new EmploymentStatisticsDto(range.From, range.To, true, sample, null, null);
        }

        var active = postings.Count(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase));
        var closed = postings.Count(p => FactJobPosting.IsTerminal(p.Status));
        return new EmploymentStatisticsDto(range.From, range.To, false, sample,
            new PostingCountsDto(postings.Count, active, closed, postings.Count(p => p.Source == "External")),
            new RegistrationCountsDto(registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "JobSeeker"),
                registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "Employer"),
                registrations.Count(r => r.Milestone == "ProfileCreated"), registrations.Count(r => r.Milestone == "AccountApproved")));
    }
}
