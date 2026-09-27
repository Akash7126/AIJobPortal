using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Application.Documents;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Resume;

public sealed record UploadResumeCommand(UploadedFile File) : JobSeekerCommand<ResumeView>;
public sealed record GetResumeMetadataQuery : JobSeekerQuery<ResumeView>;

internal sealed class UploadResumeHandler(IProfileRepository profiles, IResumeRepository resumes, IFileStorage storage, IMalwareScanner scanner,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<UploadResumeCommand, ResumeView>
{
    public async Task<Result<ResumeView>> Handle(UploadResumeCommand request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var (storageKey, sha256) = await DocumentSupport.StoreAsync(storage, scanner, $"profiles/{profile.Id}/resume", request.File, ct);
        var previous = await resumes.GetCurrentByProfileAsync(profile.Id, ct);
        previous?.MarkSuperseded();

        var file = new FileReference(storageKey, request.File.FileName, request.File.SizeBytes, request.File.ContentType, sha256);
        var resume = Domain.Resume.Upload(Guid.NewGuid(), profile.Id, file, user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        resumes.Add(resume);
        return new ResumeView(resume.Id, resume.ProfileId, resume.File.FileName, resume.File.SizeBytes, resume.Format.ToString(), resume.UploadedAtUtc);
    }
}

internal sealed class GetResumeMetadataHandler(IProfileRepository profiles, IProfileReadStore reads, ICurrentUser user)
    : IQueryHandler<GetResumeMetadataQuery, ResumeView>
{
    public async Task<Result<ResumeView>> Handle(GetResumeMetadataQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var view = await reads.GetCurrentResumeAsync(profile.Id, ct);
        return view is null ? Error.NotFound(ErrorCodes.NotFound, "No resume has been uploaded.") : view;
    }
}
