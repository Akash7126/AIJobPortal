namespace JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

public sealed record GovernmentAccessLogView(Guid Id, DateTime OccurredAtUtc, string Component, string Purpose, string SubjectRef, string Decision,
    string? ErrorCode);
