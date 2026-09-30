using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetGeographicDistributionHandler : IQueryHandler<GetGeographicDistributionQuery, GeographyDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetGeographicDistributionHandler(IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<GeographyDto>> Handle(GetGeographicDistributionQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetGeographicDistributionQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var registrations = await _analytics.RegistrationsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        return new GeographyDto(range.From, range.To, Cells(postings.Select(p => p.Location)),
            Cells(registrations.Where(r => r.Milestone == "ProfileCreated").Select(r => r.Governorate)));
    }

    /// <summary>Missing locations are counted under "unspecified" (RP.Geo.UNSPECIFIED_BUCKET); cells below MinCell are suppressed, not dropped.</summary>
    private IReadOnlyList<GeographyRowDto> Cells(IEnumerable<string?> locations) =>
        locations.GroupBy(StatisticsCalculator.Bucket).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => _policy.IsSuppressed(g.Count()) ? new GeographyRowDto(g.Key, null, true) : new GeographyRowDto(g.Key, g.Count(), false)).ToList();
}
