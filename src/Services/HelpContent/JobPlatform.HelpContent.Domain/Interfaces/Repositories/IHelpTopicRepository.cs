namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IHelpTopicRepository
{
    Task<HelpTopic?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<HelpTopic>> ListAsync(CancellationToken ct = default);

    void Add(HelpTopic topic);
}
