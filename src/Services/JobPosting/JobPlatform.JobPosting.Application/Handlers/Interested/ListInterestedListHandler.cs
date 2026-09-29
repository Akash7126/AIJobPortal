using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.DTOs.Interested;
using JobPlatform.JobPosting.Application.Queries.Interested;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Interested;

internal sealed class ListInterestedListHandler : IQueryHandler<ListInterestedListQuery, IReadOnlyList<InterestedListItemView>>
{
    private readonly IInterestedListRepository _entries;
    private readonly ICurrentUser _user;

    public ListInterestedListHandler(IInterestedListRepository entries, ICurrentUser user)
    {
        _entries = entries;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<InterestedListItemView>>> Handle(ListInterestedListQuery request, CancellationToken ct)
    {
        var list = await _entries.ListByOwnerAsync(_user.UserId!.Value, ct);
        return Result.Success<IReadOnlyList<InterestedListItemView>>(list.Select(e => new InterestedListItemView(e.Id, e.Reference.Type.ToString(),
            e.Reference.PostingId,
            e.Reference.Criteria is null
                ? null
                : new SearchCriteriaInput(e.Reference.Criteria.Keyword, e.Reference.Criteria.Governorate, e.Reference.Criteria.City,
                    e.Reference.Criteria.SalaryMin, e.Reference.Criteria.SalaryMax, e.Reference.Criteria.ContractType?.ToString(),
                    e.Reference.Criteria.PostedAfterUtc, e.Reference.Criteria.DeadlineBeforeUtc, e.Reference.Criteria.CategoryCode),
            e.CreatedAtUtc)).ToArray());
    }
}
