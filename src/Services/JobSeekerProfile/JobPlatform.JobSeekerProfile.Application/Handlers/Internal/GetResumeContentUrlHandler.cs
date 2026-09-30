using JobPlatform.JobSeekerProfile.Application.Queries.Internal;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Internal;

internal sealed class GetResumeContentUrlHandler(IResumeRepository resumes, JobPlatform.JobSeekerProfile.Application.Interfaces.IFileStorage storage)
    : IQueryHandler<GetResumeContentUrlQuery, ResumeContentUrlDto?>
{
    public async Task<Result<ResumeContentUrlDto?>> Handle(GetResumeContentUrlQuery request, CancellationToken ct)
    {
        var resume = await resumes.GetByIdAsync(request.ResumeId, ct);
        if (resume is null)
        {
            return Result.Success<ResumeContentUrlDto?>(null);
        }

        var (url, expires) = await storage.GetSignedUrlAsync(resume.File.StorageKey, TimeSpan.FromMinutes(15), ct);
        return new ResumeContentUrlDto(resume.Id, resume.ProfileId, url, resume.Format.ToString(), resume.File.SizeBytes, resume.File.Sha256, expires);
    }
}
