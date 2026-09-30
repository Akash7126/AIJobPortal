using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class GetHelpContentHandler : IQueryHandler<GetHelpContentQuery, HelpContentView>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpContentHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<HelpContentView>> Handle(GetHelpContentQuery request, CancellationToken ct) =>
        await _store.GetHelpContentAsync(request.HelpContentId, ct) is { } view ? view : Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
}
