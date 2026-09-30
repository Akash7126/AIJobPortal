using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.JobPosting.Application.Commands.Postings;

public sealed record CreateJobPostingCommand(
    string TitleAr, string TitleEn, string SummaryAr, string SummaryEn, IReadOnlyList<string> Skills, string CategoryCode, string ContractType,
    string? EducationLevel, IReadOnlyList<string>? RequiredTraining, string WorkFormat, string? Governorate, string? City, decimal? SalaryMin,
    decimal? SalaryMax, string? SalaryCurrency, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string>? RequiredLanguages,
    DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string>? OtherFields, string? IdempotencyKey)
    : EmployerCommand<PostingMutationResult>, IIdempotentCommand
{
    public JobPostingFields ToFields() => new(
        new SharedKernel.Common.ValueObjects.LocalizedText(TitleAr, TitleEn), new SharedKernel.Common.ValueObjects.LocalizedText(SummaryAr, SummaryEn),
        Skills, CategoryCode, Enum.Parse<ContractType>(ContractType, true), EducationLevel is null ? null : Enum.Parse<EducationLevel>(EducationLevel, true),
        RequiredTraining ?? Array.Empty<string>(), Enum.Parse<WorkFormat>(WorkFormat, true), Domain.JobLocation.Create(Governorate, City),
        SalaryRange.Create(SalaryMin, SalaryMax, SalaryCurrency), MinExperienceYears, MaxExperienceYears, RequiredLanguages ?? Array.Empty<string>(),
        ApplicationDeadline.Create(DeadlineUtc, AutoClose), JobLink, OtherFields);
}
