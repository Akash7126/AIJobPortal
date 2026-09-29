using JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;
using JobPlatform.GovernmentIntegration.Application.Queries.Verifications;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;

internal sealed class GetSubjectVerificationStatusHandler : IQueryHandler<GetSubjectVerificationStatusQuery, SubjectVerificationStatusView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetSubjectVerificationStatusHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<SubjectVerificationStatusView>> Handle(GetSubjectVerificationStatusQuery request, CancellationToken ct) =>
        await _store.GetSubjectVerificationStatusAsync(request.SubjectType, request.SubjectId, ct);
}
