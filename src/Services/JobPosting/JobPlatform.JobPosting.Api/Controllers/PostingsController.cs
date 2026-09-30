using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Application.Queries.Postings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.JobPosting.Api.Controllers;

/// <summary>US-3.2.1/3.2.2/3.2.4: employer job-posting management, guest/job-seeker search (handover section 6.1, base /api/v1).</summary>
[Route("api/v1")]
public sealed class PostingsController : ApiControllerBase
{
    public sealed record PostingRequest(
        string TitleAr, string TitleEn, string SummaryAr, string SummaryEn, IReadOnlyList<string> Skills, string CategoryCode, string ContractType,
        string? EducationLevel, IReadOnlyList<string>? RequiredTraining, string WorkFormat, string? Governorate, string? City, decimal? SalaryMin,
        decimal? SalaryMax, string? SalaryCurrency, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string>? RequiredLanguages,
        DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string>? OtherFields);

    public sealed record UpdatePostingRequest(
        string TitleAr, string TitleEn, string SummaryAr, string SummaryEn, IReadOnlyList<string> Skills, string CategoryCode, string ContractType,
        string? EducationLevel, IReadOnlyList<string>? RequiredTraining, string WorkFormat, string? Governorate, string? City, decimal? SalaryMin,
        decimal? SalaryMax, string? SalaryCurrency, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string>? RequiredLanguages,
        DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string>? OtherFields, string VisibilityScope,
        IReadOnlyList<Guid>? TargetJobSeekerIds);

    public sealed record RenewRequest(DateTime NewDeadlineUtc);

    public sealed record StatusRequest(string Status);

    /// <summary>US-3.2.1-01/02: create (or update the matching existing draft) a job posting.</summary>
    [HttpPost("jobs")]
    [Authorize(Policy = Policies.Employer)]
    [ForbiddenCode("E-JCP-FORBIDDEN")]
    public Task<IActionResult> Create([FromBody] PostingRequest body, CancellationToken ct)
    {
        var command = new CreateJobPostingCommand(body.TitleAr, body.TitleEn, body.SummaryAr, body.SummaryEn, body.Skills, body.CategoryCode,
            body.ContractType, body.EducationLevel, body.RequiredTraining, body.WorkFormat, body.Governorate, body.City, body.SalaryMin, body.SalaryMax,
            body.SalaryCurrency, body.MinExperienceYears, body.MaxExperienceYears, body.RequiredLanguages, body.DeadlineUtc, body.AutoClose, body.JobLink,
            body.OtherFields, IdempotencyKey);
        return Send(command, result => result.Existing ? Ok(result) : Created($"/api/v1/jobs/{result.JobPostingId}", result), ct);
    }

    /// <summary>US-3.2.1-03: owner edits the posting (details, deadline, visibility). "Later save wins": see <see cref="PostingMutationResult.Overwritten"/>.</summary>
    [HttpPut("jobs/{id:guid}")]
    [Authorize(Policy = Policies.Employer)]
    [ForbiddenCode("E-JCP-FORBIDDEN")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdatePostingRequest body, CancellationToken ct)
    {
        var command = new UpdateJobPostingCommand(id, body.TitleAr, body.TitleEn, body.SummaryAr, body.SummaryEn, body.Skills, body.CategoryCode,
            body.ContractType, body.EducationLevel, body.RequiredTraining, body.WorkFormat, body.Governorate, body.City, body.SalaryMin, body.SalaryMax,
            body.SalaryCurrency, body.MinExperienceYears, body.MaxExperienceYears, body.RequiredLanguages, body.DeadlineUtc, body.AutoClose, body.JobLink,
            body.OtherFields, body.VisibilityScope, body.TargetJobSeekerIds, IfMatch);
        return Send(command, Ok, ct);
    }

    /// <summary>US-3.2.1-04: expired -&gt; active with a new deadline (INV-07).</summary>
    [HttpPost("jobs/{id:guid}/renew")]
    [Authorize(Policy = Policies.Employer)]
    [ForbiddenCode("E-JCP-FORBIDDEN")]
    public Task<IActionResult> Renew(Guid id, [FromBody] RenewRequest body, CancellationToken ct)
    {
        return Send(new RenewJobPostingCommand(id, body.NewDeadlineUtc), Ok, ct);
    }

    /// <summary>US-3.2.4-01: explicit lifecycle transition (publish/pause/resume/expire/archive).</summary>
    [HttpPost("jobs/{id:guid}/status")]
    [Authorize(Policy = Policies.Employer)]
    [ForbiddenCode("E-JST-FORBIDDEN")]
    public Task<IActionResult> ChangeStatus(Guid id, [FromBody] StatusRequest body, CancellationToken ct)
    {
        return SendNoContent(new UpdateJobPostingStatusCommand(id, body.Status), ct);
    }

    /// <summary>US-3.2.1-03: the employer's own postings.</summary>
    [HttpGet("jobs/mine")]
    [Authorize(Policy = Policies.Employer)]
    public Task<IActionResult> Mine([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListMyJobPostingsQuery(status, page, pageSize), ct);
    }

    /// <summary>US-4.1-02: anonymous guest browsing; US-3.2.2-01/02: 8-filter search (foundation THR-013 &lt;= 2 s).</summary>
    [HttpGet("jobs/search")]
    [EnableRateLimiting("public")]
    public Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] string? governorate, [FromQuery] string? city,
        [FromQuery] decimal? salaryMin, [FromQuery] decimal? salaryMax, [FromQuery] string? contractType, [FromQuery] DateTime? postedAfterUtc,
        [FromQuery] DateTime? deadlineBeforeUtc, [FromQuery] string? categoryCode, [FromQuery] string? sort, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new SearchJobPostingsQuery(keyword, governorate, city, salaryMin, salaryMax, contractType, postedAfterUtc, deadlineBeforeUtc, categoryCode,
            sort, page, pageSize), ct);
    }

    /// <summary>US-3.2.2-02: personalised recommendations (degrades to plain search when BC-10 is unavailable).</summary>
    [HttpGet("jobs/recommended")]
    [Authorize(Policy = Policies.JobSeeker)]
    public Task<IActionResult> Recommended([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new GetRecommendedJobsQuery(page, pageSize), ct);
    }

    /// <summary>US-3.2.1-02: the field list and allowed values for the posting form.</summary>
    [HttpGet("taxonomy/posting-schema")]
    [Authorize(Policy = Policies.Employer)]
    public Task<IActionResult> Schema(CancellationToken ct)
    {
        return Send(new GetJobPostingSchemaQuery(), ct);
    }

    /// <summary>US-3.2.1-01/03/04, US-4.1-02: a single posting (guest/anonymous; visibility-filtered). ETag is the committed RowVersion,
    /// usable as If-Match on a later PUT (this is always a fresh read, unlike a mutation's own response - see <see cref="PostingMutationResult"/>).</summary>
    [HttpGet("jobs/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetJobPostingQuery(id), view =>
        {
            SetETag(view.RowVersion);
            return Ok(view);
        }, ct);
    }
}
