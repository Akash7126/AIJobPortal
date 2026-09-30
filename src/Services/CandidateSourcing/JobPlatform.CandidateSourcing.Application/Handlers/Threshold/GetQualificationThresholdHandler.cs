using JobPlatform.CandidateSourcing.Application.DTOs.Threshold;
using JobPlatform.CandidateSourcing.Application.Queries.Threshold;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Threshold;

internal sealed class GetQualificationThresholdHandler : IQueryHandler<GetQualificationThresholdQuery, ThresholdView>
{
    private readonly IQualificationThresholdRepository _thresholds;
    private readonly ICurrentUser _user;

    public GetQualificationThresholdHandler(IQualificationThresholdRepository thresholds, ICurrentUser user)
    {
        _thresholds = thresholds;
        _user = user;
    }

    public async Task<Result<ThresholdView>> Handle(GetQualificationThresholdQuery request, CancellationToken ct)
    {
        var threshold = await _thresholds.GetAsync(ActorFactory.From(_user).Id, request.JobPostingId, ct);
        return threshold is null ? new ThresholdView(request.JobPostingId, 0, 0) : new ThresholdView(threshold.JobPostingId, threshold.Percent, threshold.ThresholdVersion);
    }
}
