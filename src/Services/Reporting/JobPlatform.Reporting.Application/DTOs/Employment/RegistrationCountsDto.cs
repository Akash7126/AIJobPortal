namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record RegistrationCountsDto(long JobSeekers, long Employers, long ProfilesCreated, long AccountsApproved);
