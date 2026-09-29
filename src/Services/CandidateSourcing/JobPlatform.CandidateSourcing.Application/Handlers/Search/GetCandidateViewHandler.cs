using JobPlatform.CandidateSourcing.Application.Queries.Search;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Search;

internal sealed class GetCandidateViewHandler : IQueryHandler<GetCandidateViewQuery, CandidateViewDto>
{
    private readonly IJobSeekerProfileApi _profiles;

    public GetCandidateViewHandler(IJobSeekerProfileApi profiles) => _profiles = profiles;

    public async Task<Result<CandidateViewDto>> Handle(GetCandidateViewQuery request, CancellationToken ct)
    {
        var view = await _profiles.GetCandidateViewAsync(request.CandidateProfileId, ct);
        if (view is null || view.Deactivated || (view.Visibility == "Private" && !view.EmployerVisibilityOptIn))
        {
            return Error.NotFound(ErrorCodes.NotFound, "The candidate was not found or is not visible.");
        }

        return view;
    }
}
