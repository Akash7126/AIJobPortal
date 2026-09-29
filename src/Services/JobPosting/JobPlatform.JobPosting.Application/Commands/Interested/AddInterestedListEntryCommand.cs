using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Commands.Interested;

public sealed record AddInterestedListEntryCommand(
    string ReferenceType, Guid? PostingId, string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax,
    string? ContractType, string? CategoryCode) : JobSeekerInterestedCommand<Guid>
{
    public InterestedReference ToReference() => Enum.Parse<InterestedReferenceType>(ReferenceType, true) == InterestedReferenceType.Posting
        ? InterestedReference.ToPosting(PostingId!.Value)
        : InterestedReference.ToFilter(new SearchCriteria(Keyword, Governorate, City, SalaryMin, SalaryMax,
            ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), null, null, CategoryCode));
}
