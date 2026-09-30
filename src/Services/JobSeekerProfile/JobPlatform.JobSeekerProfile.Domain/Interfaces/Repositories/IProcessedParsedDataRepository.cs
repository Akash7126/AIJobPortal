namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IProcessedParsedDataRepository
{
    Task<bool> ExistsAsync(Guid resumeParsedDataId, CancellationToken ct = default);
    void Add(ProcessedParsedData record);
}
