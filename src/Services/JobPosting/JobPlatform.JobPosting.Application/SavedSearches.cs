using FluentValidation;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application;

public sealed record SaveSearchCommand(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode, bool NotifyOnMatch) : JobSeekerCommand<SavedSearchView>
{
    public SearchCriteria ToDomain() => new(Keyword, Governorate, City, SalaryMin, SalaryMax,
        ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}

public sealed record UpdateSavedSearchCommand(Guid SavedSearchId, bool NotifyOnMatch) : JobSeekerCommand<Unit>;

public sealed record DeleteSavedSearchCommand(Guid SavedSearchId) : JobSeekerCommand<Unit>;

public sealed record ListSavedSearchesQuery : JobSeekerQuery<IReadOnlyList<SavedSearchView>>;

public sealed class SaveSearchValidator : AbstractValidator<SaveSearchCommand>
{
    public SaveSearchValidator()
    {
        RuleFor(c => c).Must(c => c.Keyword is not null || c.Governorate is not null || c.City is not null || c.SalaryMin is not null
                || c.SalaryMax is not null || c.ContractType is not null || c.CategoryCode is not null || c.PostedAfterUtc is not null
                || c.DeadlineBeforeUtc is not null)
            .WithErrorCode("VAL.Criteria.AtLeastOneRequired");
        RuleFor(c => c).Must(c => c.SalaryMin is null || c.SalaryMax is null || c.SalaryMin <= c.SalaryMax)
            .WithErrorCode(ErrorCodes.SearchInvalidField).WithName("salary");
        RuleFor(c => c.ContractType).Must(v => v is null || Enum.TryParse<ContractType>(v, true, out _)).WithErrorCode("VAL.ContractType.Invalid");
    }
}

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

internal sealed class UpdateSavedSearchHandler : ICommandHandler<UpdateSavedSearchCommand, Unit>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public UpdateSavedSearchHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateSavedSearchCommand request, CancellationToken ct)
    {
        var search = await _searches.GetByIdAsync(request.SavedSearchId, ct);
        if (search is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The saved search was not found.");
        }

        search.SetNotify(request.NotifyOnMatch, ActorFactory.From(_user));
        return Result.Success();
    }
}

internal sealed class DeleteSavedSearchHandler : ICommandHandler<DeleteSavedSearchCommand, Unit>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public DeleteSavedSearchHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteSavedSearchCommand request, CancellationToken ct)
    {
        var search = await _searches.GetByIdAsync(request.SavedSearchId, ct);
        if (search is null)
        {
            return Result.Success();
        }

        if (search.OwnerAccountId != _user.UserId)
        {
            return Error.Forbidden(ErrorCodes.FavoriteForbidden, "Only the owner may delete this saved search.");
        }

        _searches.Remove(search);
        return Result.Success();
    }
}

internal sealed class ListSavedSearchesHandler : IQueryHandler<ListSavedSearchesQuery, IReadOnlyList<SavedSearchView>>
{
    private readonly ISavedSearchRepository _searches;
    private readonly ICurrentUser _user;

    public ListSavedSearchesHandler(ISavedSearchRepository searches, ICurrentUser user)
    {
        _searches = searches;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<SavedSearchView>>> Handle(ListSavedSearchesQuery request, CancellationToken ct)
    {
        var list = await _searches.ListByOwnerAsync(_user.UserId!.Value, ct);
        return Result.Success<IReadOnlyList<SavedSearchView>>(list.Select(SaveSearchHandler.ToView).ToArray());
    }
}
