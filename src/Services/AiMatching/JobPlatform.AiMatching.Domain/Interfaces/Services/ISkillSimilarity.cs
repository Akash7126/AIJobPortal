namespace JobPlatform.AiMatching.Domain.Interfaces.Services;

/// <summary>Similarity of two skill/training terms in 0..1 (1 = same). Lets the engine stay pure while an embedding model enriches near-synonyms.</summary>
public interface ISkillSimilarity
{
    double Between(string a, string b);
}
