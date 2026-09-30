using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class ParsedProfileDataRepository(AiMatchingDbContext db) : IParsedProfileDataRepository
{
    public Task<ParsedProfileData?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.ParsedProfileData.Include(p => p.Fields).FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public Task<ParsedProfileData?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.ParsedProfileData.Include(p => p.Fields).FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(ParsedProfileData data) => db.ParsedProfileData.Add(data);
}
