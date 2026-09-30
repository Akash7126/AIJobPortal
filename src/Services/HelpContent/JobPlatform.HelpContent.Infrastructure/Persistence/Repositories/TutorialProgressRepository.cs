using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class TutorialProgressRepository(HelpContentDbContext db) : ITutorialProgressRepository
{
    public Task<TutorialProgress?> GetAsync(Guid userId, Guid tutorialId, CancellationToken ct = default) =>
        db.TutorialProgresses.FirstOrDefaultAsync(p => p.UserId == userId && p.TutorialId == tutorialId, ct);

    public void Add(TutorialProgress progress) => db.TutorialProgresses.Add(progress);
}
