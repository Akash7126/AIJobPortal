using JobPlatform.CandidateSourcing.Application.Commands.Threshold;
using JobPlatform.CandidateSourcing.Application.DTOs.Threshold;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Threshold;

internal sealed class SetQualificationThresholdHandler : ICommandHandler<SetQualificationThresholdCommand, ThresholdView>
{
    private readonly IQualificationThresholdRepository _thresholds;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SetQualificationThresholdHandler(IQualificationThresholdRepository thresholds, ICurrentUser user, TimeProvider clock)
    {
        _thresholds = thresholds;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<ThresholdView>> Handle(SetQualificationThresholdCommand request, CancellationToken ct)
    {
        var employerId = ActorFactory.From(_user).Id;
        var threshold = await _thresholds.GetAsync(employerId, request.JobPostingId, ct);
        if (threshold is null)
        {
            threshold = Domain.Threshold.QualificationThreshold.Open(employerId, request.JobPostingId, _clock.GetUtcNow().UtcDateTime);
            _thresholds.Add(threshold);
        }

        threshold.Set(request.Percent, employerId, _clock.GetUtcNow().UtcDateTime);
        return new ThresholdView(threshold.JobPostingId, threshold.Percent, threshold.ThresholdVersion);
    }
}
