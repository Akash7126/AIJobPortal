using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.JobPosting.Application.Commands.Favorites;
using JobPlatform.JobPosting.Application.Commands.Interested;
using JobPlatform.JobPosting.Application.Commands.SavedSearches;
using JobPlatform.JobPosting.Application.Queries.Favorites;
using JobPlatform.JobPosting.Application.Queries.Interested;
using JobPlatform.JobPosting.Application.Queries.SavedSearches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobPosting.Api.Controllers;

/// <summary>Job-seeker favourites, saved searches and interested list (handover section 6.1). Every route requires the JobSeeker role.</summary>
[Route("api/v1")]
[Authorize(Policy = Policies.JobSeeker)]
public sealed class JobSeekerController : ApiControllerBase
{
    public sealed record SavedSearchRequest(
        string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
        DateTime? DeadlineBeforeUtc, string? CategoryCode, bool NotifyOnMatch);

    public sealed record NotifyRequest(bool NotifyOnMatch);

    public sealed record InterestedRequest(
        string ReferenceType, Guid? PostingId, string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax,
        string? ContractType, string? CategoryCode);

    /// <summary>US-3.2.2-03: heart-button toggle.</summary>
    [HttpPut("favorites/{jobId:guid}")]
    [ForbiddenCode("E-JSF-FORBIDDEN")]
    public Task<IActionResult> ToggleFavorite(Guid jobId, CancellationToken ct)
    {
        var command = new ToggleFavoriteJobCommand(jobId);
        return Send(command, favorited => Ok(new { favorited }), ct);
    }

    [HttpGet("favorites")]
    public Task<IActionResult> ListFavorites(CancellationToken ct)
    {
        var query = new ListFavoritesQuery();
        return Send(query, ct);
    }

    /// <summary>US-3.2.2-04: save a search (an identical one is reused).</summary>
    [HttpPost("saved-searches")]
    [ForbiddenCode("E-JSF-FORBIDDEN")]
    public Task<IActionResult> SaveSearch([FromBody] SavedSearchRequest body, CancellationToken ct)
    {
        var command = new SaveSearchCommand(body.Keyword, body.Governorate, body.City, body.SalaryMin, body.SalaryMax, body.ContractType, body.PostedAfterUtc,
            body.DeadlineBeforeUtc, body.CategoryCode, body.NotifyOnMatch);
        return Send(command, result => Ok(result), ct);
    }

    [HttpGet("saved-searches")]
    public Task<IActionResult> ListSavedSearches(CancellationToken ct)
    {
        var query = new ListSavedSearchesQuery();
        return Send(query, ct);
    }

    [HttpPatch("saved-searches/{id:guid}")]
    [ForbiddenCode("E-JSF-FORBIDDEN")]
    public Task<IActionResult> UpdateSavedSearch(Guid id, [FromBody] NotifyRequest body, CancellationToken ct)
    {
        var command = new UpdateSavedSearchCommand(id, body.NotifyOnMatch);
        return SendNoContent(command, ct);
    }

    [HttpDelete("saved-searches/{id:guid}")]
    [ForbiddenCode("E-JSF-FORBIDDEN")]
    public Task<IActionResult> DeleteSavedSearch(Guid id, CancellationToken ct)
    {
        var command = new DeleteSavedSearchCommand(id);
        return SendNoContent(command, ct);
    }

    /// <summary>US-3.2.3-01: bookmark a posting or a stored filter.</summary>
    [HttpPost("interested")]
    [ForbiddenCode("E-JIP-FORBIDDEN")]
    public Task<IActionResult> AddInterested([FromBody] InterestedRequest body, CancellationToken ct)
    {
        var command = new AddInterestedListEntryCommand(body.ReferenceType, body.PostingId, body.Keyword, body.Governorate, body.City, body.SalaryMin,
            body.SalaryMax, body.ContractType, body.CategoryCode);
        return SendCreated(command, id => $"/api/v1/interested/{id}", ct);
    }

    [HttpGet("interested")]
    public Task<IActionResult> ListInterested(CancellationToken ct)
    {
        var query = new ListInterestedListQuery();
        return Send(query, ct);
    }

    [HttpDelete("interested/{id:guid}")]
    [ForbiddenCode("E-JIP-FORBIDDEN")]
    public Task<IActionResult> DeleteInterested(Guid id, CancellationToken ct)
    {
        var command = new DeleteInterestedListEntryCommand(id);
        return SendNoContent(command, ct);
    }
}
