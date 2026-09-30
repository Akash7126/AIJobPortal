using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class HelpFeedbackRepository(HelpContentDbContext db) : IHelpFeedbackRepository
{
    public Task<HelpFeedback?> GetByUserAndContentAsync(Guid userId, Guid helpContentId, CancellationToken ct = default) =>
        db.HelpFeedbacks.FirstOrDefaultAsync(f => f.UserId == userId && f.HelpContentId == helpContentId, ct);

    public void Add(HelpFeedback feedback) => db.HelpFeedbacks.Add(feedback);
}
