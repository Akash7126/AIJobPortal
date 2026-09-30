namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IHelpContentRepository
{
    Task<HelpContent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    void Add(HelpContent content);
}
