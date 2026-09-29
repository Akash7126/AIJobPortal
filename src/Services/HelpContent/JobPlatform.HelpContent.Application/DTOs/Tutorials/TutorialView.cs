using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.Tutorials;

public sealed record TutorialView(Guid TutorialId, LocalizedView Title, LocalizedView Body, bool Completed, DateTime? CompletedAtUtc);
