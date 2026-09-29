namespace JobPlatform.Reporting.Application.DTOs.Activity;

public sealed record ActiveSession(string SessionKey, string ActorType, DateTime StartedAtUtc, DateTime LastSeenAtUtc, string ProfileLink);
