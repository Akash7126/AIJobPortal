using JobPlatform.HelpContent.Application.DTOs.Feedback;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.Feedback;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Feedback;

internal sealed class GetHelpFeedbackSummaryHandler : IQueryHandler<GetHelpFeedbackSummaryQuery, HelpFeedbackSummaryView>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpFeedbackSummaryHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<HelpFeedbackSummaryView>> Handle(GetHelpFeedbackSummaryQuery request, CancellationToken ct) =>
        await _store.GetFeedbackSummaryAsync(request.HelpContentId, ct) ?? new HelpFeedbackSummaryView(request.HelpContentId, 0, 0);
}
