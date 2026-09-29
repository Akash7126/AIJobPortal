namespace JobPlatform.AiMatching.Application.DTOs.Shortlists;

public sealed record ShortlistDto(
    Guid Id, Guid JobPostingId, Guid EmployerAccountId, string Status, int RequestedSize, int ConfigVersion, IReadOnlyList<ShortlistEntryDto> Items,
    string? FailureReason, DateTime RequestedAtUtc, DateTime? ComputedAtUtc);
