namespace JobPlatform.AiMatching.Application.DTOs.Shortlists;

public sealed record ShortlistEntryDto(int Rank, Guid ProfileId, decimal Score);
