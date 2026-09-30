namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IContentCategoryRepository
{
    Task<ContentCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ContentCategory>> ListAsync(CancellationToken ct = default);

    void Add(ContentCategory category);
}
