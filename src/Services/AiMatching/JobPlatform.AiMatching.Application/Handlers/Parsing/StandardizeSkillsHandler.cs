using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Parsing;

internal sealed class StandardizeSkillsHandler(ISkillStandardizationRepository standardizations, IResumeParsedDataRepository parsed, ISkillTaxonomyProvider taxonomies,
    TimeProvider clock) : ICommandHandler<StandardizeSkillsCommand, int>
{
    public async Task<Result<int>> Handle(StandardizeSkillsCommand request, CancellationToken ct)
    {
        var taxonomy = await taxonomies.GetAsync(null, ct); // captured once: the whole run keeps this version
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ResumeParsedDataId is { } id)
        {
            var data = await parsed.GetAsync(id, ct);
            if (data is null)
            {
                return Error.NotFound(AiErrorCodes.NotFound, "The parsed data was not found.");
            }

            var existing = await standardizations.GetByResumeParsedDataAsync(id, ct);
            if (existing is null)
            {
                standardizations.Add(SkillStandardization.Standardize(id, data.ProfileId, data.Skills, taxonomy, now));
                return 1;
            }

            return existing.Restandardize(taxonomy, now) ? 1 : 0;
        }

        var stale = await standardizations.ListNotOnTaxonomyVersionAsync(taxonomy.Version, request.BatchSize, ct);
        return stale.Count(run => run.Restandardize(taxonomy, now));
    }
}
