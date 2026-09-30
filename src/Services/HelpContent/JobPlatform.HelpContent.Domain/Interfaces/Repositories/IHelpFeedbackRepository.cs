namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IHelpFeedbackRepository
{
    Task<HelpFeedback?> GetByUserAndContentAsync(Guid userId, Guid helpContentId, CancellationToken ct = default);

    void Add(HelpFeedback feedback);
}
