using JobPlatform.AiMatching.Application.Commands.Semantics;
using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Services.Semantics;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Application.Handlers.Semantics;

internal sealed class AnalyzeJobDescriptionHandler(IPostingDirectory postings, PostingAnalysisService analysis, ILogger<AnalyzeJobDescriptionHandler> logger)
    : ICommandHandler<AnalyzeJobDescriptionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AnalyzeJobDescriptionCommand request, CancellationToken ct)
    {
        PostingSource source = request.Seed;
        try
        {
            source = await postings.GetAsync(request.Seed.JobPostingId, ct) ?? request.Seed;
        }
        catch (UpstreamUnavailableException ex)
        {
            logger.LogWarning(ex, "Posting {PostingId} could not be fetched; analysing the event data only", request.Seed.JobPostingId);
        }

        await analysis.AnalyzeAsync(source, ct);
        return Result.Success();
    }
}
