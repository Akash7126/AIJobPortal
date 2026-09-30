namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface ITutorialProgressRepository
{
    Task<TutorialProgress?> GetAsync(Guid userId, Guid tutorialId, CancellationToken ct = default);

    void Add(TutorialProgress progress);
}
