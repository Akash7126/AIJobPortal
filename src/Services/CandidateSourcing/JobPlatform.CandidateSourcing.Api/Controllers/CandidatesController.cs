using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.Queries.Insight;
using JobPlatform.CandidateSourcing.Application.Queries.Search;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

/// <summary>US-3.3.3-04/05/06: the candidate database, one candidate's privacy-filtered view, and candidate insight.</summary>
public sealed class CandidatesController : CandidateSourcingControllerBase
{
    public sealed record SearchRequest(
        IReadOnlyList<string>? Skills, string? EducationLevel, decimal? MinExperienceYears, decimal? MaxExperienceYears, string? LocationCode,
        decimal? SalaryMin, decimal? SalaryMax, string? Availability, int Page = 1, int PageSize = 20);

    [HttpPost("candidates/search")]
    public Task<IActionResult> Search([FromBody] SearchRequest body, CancellationToken ct)
    {
        var criteria = new CandidateSearchCriteria(body.Skills, body.EducationLevel, body.MinExperienceYears, body.MaxExperienceYears, body.LocationCode,
            body.SalaryMin, body.SalaryMax, body.Availability);
        return Send(new SearchCandidateDatabaseQuery(criteria, body.Page, body.PageSize), ct);
    }

    [HttpGet("candidates/{candidateId:guid}")]
    public Task<IActionResult> Get(Guid candidateId, CancellationToken ct) => Send(new GetCandidateViewQuery(candidateId), ct);

    [HttpGet("candidates/{candidateId:guid}/insight")]
    public Task<IActionResult> Insight(Guid candidateId, [FromQuery] Guid jobPostingId, CancellationToken ct) =>
        Send(new GetCandidateInsightQuery(candidateId, jobPostingId), ct);
}
