using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.JobSeekerProfile.Application.Commands.Documents;
using JobPlatform.JobSeekerProfile.Application.Commands.Resume;
using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.Queries.Documents;
using JobPlatform.JobSeekerProfile.Application.Queries.Resume;
using JobPlatform.JobSeekerProfile.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobSeekerProfile.Api.Controllers;

/// <summary>US-3.1.1-09 (job seeker documents) and US-3.1.1-10 (resume). JobSeeker role.</summary>
[Route("api/v1/profiles/me")]
[Authorize(Policy = Policies.JobSeeker)]
[ForbiddenCode(ErrorCodes.Forbidden)]
public sealed class DocumentsController : ApiControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    [HttpPost("documents")]
    [RequestSizeLimit(MaxUploadBytes + 1024)]
    public async Task<IActionResult> AttachDocument(IFormFile file, [FromForm] string documentType, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var uploaded = new UploadedFile(file.FileName, file.ContentType, file.Length, stream);
        return await Send(new AttachSupplementaryDocumentCommand(uploaded, documentType), v => Created($"/api/v1/profiles/me/documents/{v.DocumentId}", v), ct);
    }

    [HttpGet("documents")]
    public Task<IActionResult> ListDocuments(CancellationToken ct) => Send(new ListSupplementaryDocumentsQuery(), ct);

    [HttpDelete("documents/{id:guid}")]
    public Task<IActionResult> RemoveDocument(Guid id, CancellationToken ct) => SendNoContent(new RemoveSupplementaryDocumentCommand(id), ct);

    [HttpPost("resume")]
    [RequestSizeLimit(MaxUploadBytes + 1024)]
    public async Task<IActionResult> UploadResume(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var uploaded = new UploadedFile(file.FileName, file.ContentType, file.Length, stream);
        return await Send(new UploadResumeCommand(uploaded), v => Created($"/api/v1/profiles/me/resume", v), ct);
    }

    [HttpGet("resume")]
    public Task<IActionResult> GetResume(CancellationToken ct) => Send(new GetResumeMetadataQuery(), ct);
}

/// <summary>US-3.1.2-08: employer supplementary documents. Implemented here per the pipeline's BC assignment (handover Q-02 recommends moving to BC-05).</summary>
[Route("api/v1/companies/{companyId:guid}")]
[Authorize(Policy = Policies.Employer)]
[ForbiddenCode(ErrorCodes.CompanyForbidden)]
public sealed class CompanyDocumentsController : ApiControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    [HttpPost("documents")]
    [RequestSizeLimit(MaxUploadBytes + 1024)]
    public async Task<IActionResult> AttachDocument(Guid companyId, IFormFile file, [FromForm] string documentType, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var uploaded = new UploadedFile(file.FileName, file.ContentType, file.Length, stream);
        return await Send(new AttachCompanyDocumentCommand(uploaded, documentType), v => Created($"/api/v1/companies/{companyId}/documents/{v.DocumentId}", v),
            ct);
    }
}
