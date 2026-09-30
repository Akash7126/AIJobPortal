namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IContextHelpMappingRepository
{
    Task<ContextHelpMapping?> GetByPageKeyAsync(string pageKey, CancellationToken ct = default);

    void Add(ContextHelpMapping mapping);
}
