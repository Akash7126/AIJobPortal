using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.JobSeekerProfile.Application.Commands.Preferences;
using JobPlatform.JobSeekerProfile.Application.Commands.Privacy;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Application.Queries.Preferences;
using JobPlatform.JobSeekerProfile.Application.Queries.Privacy;
using JobPlatform.JobSeekerProfile.Application.Queries.Profile;
using JobPlatform.JobSeekerProfile.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobSeekerProfile.Api.Controllers;

/// <summary>Job-seeker-owner surface (JobSeeker role). Every refusal carries E-JSRPM-FORBIDDEN.</summary>
[Route("api/v1")]
[Authorize(Policy = Policies.JobSeeker)]
[ForbiddenCode(ErrorCodes.Forbidden)]
public sealed class ProfileController : ApiControllerBase
{
    [HttpPost("profiles")]
    public Task<IActionResult> Create([FromBody] CreateProfileCommand body, CancellationToken ct)
    {
        return Send(body, v => Created($"/api/v1/profiles/me", v), ct);
    }

    [HttpGet("profiles/me")]
    public Task<IActionResult> GetMine(CancellationToken ct)
    {
        var query = new GetMyProfileQuery();
        return Send(query, v =>
        {
            SetETag(v.RowVersion);
            return Ok(v);
        }, ct);
    }

    [HttpGet("profiles/me/completion")]
    public Task<IActionResult> GetCompletion(CancellationToken ct)
    {
        var query = new GetProfileCompletionRecommendationQuery();
        return Send(query, ct);
    }

    [HttpPut("profiles/me/level1")]
    public Task<IActionResult> UpdateLevel1([FromBody] Level1Request body, CancellationToken ct)
    {
        var command = new UpdateLevel1Command(body.FullName, body.Email, body.MobileNumber, body.Gender, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/education")]
    public Task<IActionResult> UpdateEducation([FromBody] IReadOnlyList<EducationInput> body, CancellationToken ct)
    {
        var command = new UpdateEducationCommand(body, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/experience")]
    public Task<IActionResult> UpdateExperience([FromBody] ExperienceRequest body, CancellationToken ct)
    {
        var command = new UpdateExperienceCommand(body.Entries, body.YearsOfExperience, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/skills")]
    public Task<IActionResult> UpdateSkills([FromBody] IReadOnlyList<SkillInput> body, CancellationToken ct)
    {
        var command = new UpdateSkillsCommand(body, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/training")]
    public Task<IActionResult> UpdateTraining([FromBody] IReadOnlyList<TrainingInput> body, CancellationToken ct)
    {
        var command = new UpdateTrainingCommand(body, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/certificates")]
    public Task<IActionResult> UpdateCertificates([FromBody] IReadOnlyList<CertificateInput> body, CancellationToken ct)
    {
        var command = new UpdateCertificatesCommand(body, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/level3")]
    public Task<IActionResult> UpdateLevel3([FromBody] Level3Request body, CancellationToken ct)
    {
        var command = new UpdateLevel3Command(body.SocialLinks, body.Statement, body.Bio, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpPut("profiles/me/job-preference")]
    public Task<IActionResult> ConfigureJobPreference([FromBody] ConfigureJobPreferenceCommand body, CancellationToken ct)
    {
        return SendNoContent(body, ct);
    }

    [HttpGet("profiles/me/job-preference")]
    public Task<IActionResult> GetJobPreference(CancellationToken ct)
    {
        var query = new GetJobPreferenceQuery();
        return Send(query, ct);
    }

    [HttpPut("profiles/me/privacy/visibility")]
    public Task<IActionResult> SetVisibility([FromBody] SetProfileVisibilityCommand body, CancellationToken ct)
    {
        return SendNoContent(body, ct);
    }

    [HttpGet("profiles/me/privacy")]
    public Task<IActionResult> GetPrivacy(CancellationToken ct)
    {
        var query = new GetPrivacySettingQuery();
        return Send(query, ct);
    }

    [HttpPost("profiles/me/privacy/deactivation")]
    public Task<IActionResult> RequestDeactivation([FromBody] RequestAccountDeactivationCommand body, CancellationToken ct)
    {
        return Send(body, _ => Accepted(), ct);
    }

    [HttpPost("profiles/me/privacy/deletion")]
    public Task<IActionResult> RequestDeletion([FromBody] RequestAccountDeletionCommand body, CancellationToken ct)
    {
        return Send(body, _ => Accepted(), ct);
    }

    public sealed record Level1Request(string FullName, string Email, string MobileNumber, string Gender);
    public sealed record ExperienceRequest(IReadOnlyList<ExperienceInput> Entries, decimal? YearsOfExperience);
    public sealed record Level3Request(IReadOnlyList<SocialLinkInput> SocialLinks, string? Statement, string? Bio);
}
