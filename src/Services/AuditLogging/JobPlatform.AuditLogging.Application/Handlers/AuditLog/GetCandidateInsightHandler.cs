using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetCandidateInsightHandler : IQueryHandler<GetCandidateInsightQuery, CandidateInsightDto>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public GetCandidateInsightHandler(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<CandidateInsightDto>> Handle(GetCandidateInsightQuery q, CancellationToken ct)
    {
        var raw = await _store.GetCandidateInsightAsync(q.CandidateId, q.JobPostingId, ct);
        if (raw is null)
        {
            return Error.NotFound(AuditErrorCodes.InsightNotFound, "No insight is available for this candidate and posting.");
        }

        AccessScopePolicy.EnsureCanView(AuditCategory.Insight, new Viewer(_user.ActorType, _user.UserId), OwnerScope.Of(OwnerType.Employer, raw.EmployerId));
        return new CandidateInsightDto(q.CandidateId, q.JobPostingId,
            CandidateInsightPolicy.Availability(raw.Availability, raw.Withheld),
            CandidateInsightPolicy.ExpectedSalary(raw.ExpectedSalary, raw.Withheld),
            CandidateInsightPolicy.Fit(raw.FitScore, raw.Withheld),
            raw.ComputedAtUtc);
    }
}
