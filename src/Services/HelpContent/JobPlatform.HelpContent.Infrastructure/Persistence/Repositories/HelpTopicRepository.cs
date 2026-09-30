using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class HelpTopicRepository(HelpContentDbContext db) : IHelpTopicRepository
{
    public Task<HelpTopic?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.HelpTopics.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<HelpTopic>> ListAsync(CancellationToken ct = default) => await db.HelpTopics.ToListAsync(ct);

    public void Add(HelpTopic topic) => db.HelpTopics.Add(topic);
}
