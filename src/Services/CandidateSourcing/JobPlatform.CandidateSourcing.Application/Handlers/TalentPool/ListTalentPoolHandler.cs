using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.CandidateSourcing.Application.Queries.TalentPool;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.TalentPool;

internal sealed class ListTalentPoolHandler : IQueryHandler<ListTalentPoolQuery, IReadOnlyList<TalentPoolEntryView>>
{
    private readonly ICandidateSourcingReadStore _store;
    private readonly ICurrentUser _user;

    public ListTalentPoolHandler(ICandidateSourcingReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<TalentPoolEntryView>>> Handle(ListTalentPoolQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListTalentPoolAsync(ActorFactory.From(_user).Id, ct));
}
