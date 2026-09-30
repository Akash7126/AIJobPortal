namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface ISkillStandardizationRepository
{
    Task<SkillStandardization?> GetByResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default);

    Task<IReadOnlyList<SkillStandardization>> ListNotOnTaxonomyVersionAsync(string taxonomyVersion, int take, CancellationToken ct = default);

    void Add(SkillStandardization standardization);
}
