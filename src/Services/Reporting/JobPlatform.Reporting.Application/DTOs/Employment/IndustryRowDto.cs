namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record IndustryRowDto(string Industry, long? Demand, long? ActivePostings, bool InsufficientData);
