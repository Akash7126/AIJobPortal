using FluentValidation;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Threshold;

public sealed record ThresholdView(Guid JobPostingId, int Percent, int Version);

/// <summary>US-3.3.3-03: set the per-posting qualification cutoff. INV-06 (snapshot-at-run-start) is honoured by the read handlers, which capture
/// <see cref="ThresholdView.Percent"/> once at the start of their computation.</summary>
public sealed record SetQualificationThresholdCommand(Guid JobPostingId, int Percent) : EmployerCommand<ThresholdView>;

public sealed class SetQualificationThresholdValidator : AbstractValidator<SetQualificationThresholdCommand>
{
    public SetQualificationThresholdValidator()
    {
        RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(c => c.Percent).InclusiveBetween(0, 100).WithErrorCode("VAL.Percent.OutOfRange");
    }
}

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

public sealed record GetQualificationThresholdQuery(Guid JobPostingId) : EmployerQuery<ThresholdView>;

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
