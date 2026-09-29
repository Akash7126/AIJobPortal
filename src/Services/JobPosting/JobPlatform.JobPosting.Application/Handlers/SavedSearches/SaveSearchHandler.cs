using JobPlatform.JobPosting.Application.Commands.SavedSearches;
using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.DTOs.SavedSearches;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.SavedSearches;

internal sealed class SaveSearchHandler : ICommandHandler<SaveSearchCommand, SavedSearchView>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SaveSearchHandler(ISavedSearchRepository searches, ICurrentUser user, TimeProvider clock)
    {
        _searches = searches;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<SavedSearchView>> Handle(SaveSearchCommand request, CancellationToken ct)
    {
        var owner = _user.UserId!.Value;
        var criteria = request.ToDomain();
        var hash = SavedSearch.Hash(criteria);
        var existing = await _searches.GetByHashAsync(owner, hash, ct);
        if (existing is not null)
        {
            existing.SetNotify(request.NotifyOnMatch, ActorFactory.From(_user));
            return ToView(existing);
        }

        var search = SavedSearch.Save(owner, criteria, request.NotifyOnMatch, _clock.GetUtcNow().UtcDateTime);
        _searches.Add(search);
        return ToView(search);
    }

    internal static SavedSearchView ToView(SavedSearch s) => new(s.Id,
        new SearchCriteriaInput(s.Criteria.Keyword, s.Criteria.Governorate, s.Criteria.City, s.Criteria.SalaryMin, s.Criteria.SalaryMax,
            s.Criteria.ContractType?.ToString(), s.Criteria.PostedAfterUtc, s.Criteria.DeadlineBeforeUtc, s.Criteria.CategoryCode),
        s.NotifyOnMatch, s.CreatedAtUtc, s.LastEvaluatedAtUtc);
}
