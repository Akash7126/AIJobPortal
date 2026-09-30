using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Interfaces;

public interface ISemanticAnalyzer
{
    Task<SemanticAnalysis> AnalyzeAsync(string title, string? description, IReadOnlyList<string> declaredSkills, string category, SkillTaxonomy taxonomy,
        CancellationToken ct = default);
}
