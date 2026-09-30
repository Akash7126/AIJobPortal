namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IParsedProfileDataRepository
{
    Task<ParsedProfileData?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);

    Task<ParsedProfileData?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    void Add(ParsedProfileData data);
}
