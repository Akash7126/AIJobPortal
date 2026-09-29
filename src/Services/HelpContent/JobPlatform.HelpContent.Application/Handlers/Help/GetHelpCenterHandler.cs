using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class GetHelpCenterHandler : IQueryHandler<GetHelpCenterQuery, IReadOnlyList<HelpCenterTopicView>>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpCenterHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<HelpCenterTopicView>>> Handle(GetHelpCenterQuery request, CancellationToken ct) =>
        Result.Success(await _store.GetHelpCenterAsync(request.Role, ct));
}
