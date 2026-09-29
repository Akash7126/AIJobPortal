namespace JobPlatform.AiMatching.Application.DTOs.Configuration;

public sealed record WeightsDto(decimal SkillOverlap, decimal Education, decimal Training, decimal Location, decimal Experience, decimal Salary);
