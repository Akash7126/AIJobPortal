using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class HelpOrganizationRepository(HelpContentDbContext db) : IHelpOrganizationRepository
{
    public Task<HelpContentOrganization?> GetAsync(Guid helpContentId, CancellationToken ct = default) =>
        db.HelpContentOrganizations.FirstOrDefaultAsync(o => o.HelpContentId == helpContentId, ct);

    public void Add(HelpContentOrganization organization) => db.HelpContentOrganizations.Add(organization);
}
