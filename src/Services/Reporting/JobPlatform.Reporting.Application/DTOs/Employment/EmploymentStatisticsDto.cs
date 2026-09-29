namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record EmploymentStatisticsDto(DateOnly From, DateOnly To, bool InsufficientData, long SampleSize, PostingCountsDto? Postings, RegistrationCountsDto? Registrations);
