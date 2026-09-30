namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IHelpOrganizationRepository
{
    Task<HelpContentOrganization?> GetAsync(Guid helpContentId, CancellationToken ct = default);

    void Add(HelpContentOrganization organization);
}
