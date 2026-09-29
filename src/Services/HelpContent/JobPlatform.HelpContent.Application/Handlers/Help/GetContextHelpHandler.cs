using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class GetContextHelpHandler : IQueryHandler<GetContextHelpQuery, HelpContentView?>
{
    private readonly IContextHelpMappingRepository _mappings;
    private readonly IHelpContentReadStore _store;

    public GetContextHelpHandler(IContextHelpMappingRepository mappings, IHelpContentReadStore store)
    {
        _mappings = mappings;
        _store = store;
    }

    public async Task<Result<HelpContentView?>> Handle(GetContextHelpQuery request, CancellationToken ct)
    {
        var mapping = await _mappings.GetByPageKeyAsync(request.PageKey, ct);
        return mapping is null ? Result.Success<HelpContentView?>(null) : Result.Success(await _store.GetHelpContentAsync(mapping.HelpContentId, ct));
    }
}
