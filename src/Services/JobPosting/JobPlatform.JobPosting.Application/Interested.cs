using FluentValidation;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application;

public sealed record AddInterestedListEntryCommand(
    string ReferenceType, Guid? PostingId, string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax,
    string? ContractType, string? CategoryCode) : JobSeekerInterestedCommand<Guid>
{
    public InterestedReference ToReference() => Enum.Parse<InterestedReferenceType>(ReferenceType, true) == InterestedReferenceType.Posting
        ? InterestedReference.ToPosting(PostingId!.Value)
        : InterestedReference.ToFilter(new SearchCriteria(Keyword, Governorate, City, SalaryMin, SalaryMax,
            ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), null, null, CategoryCode));
}

public sealed record DeleteInterestedListEntryCommand(Guid InterestedListEntryId) : JobSeekerInterestedCommand<Unit>;

public sealed record ListInterestedListQuery : JobSeekerQuery<IReadOnlyList<InterestedListItemView>>;

public sealed class AddInterestedListEntryValidator : AbstractValidator<AddInterestedListEntryCommand>
{
    public AddInterestedListEntryValidator()
    {
        RuleFor(c => c.ReferenceType).Must(v => Enum.TryParse<InterestedReferenceType>(v, true, out _)).WithErrorCode("VAL.ReferenceType.Invalid");
        RuleFor(c => c.PostingId).NotNull().WithErrorCode("VAL.PostingId.Required")
            .When(c => Enum.TryParse<InterestedReferenceType>(c.ReferenceType, true, out var t) && t == InterestedReferenceType.Posting);
        RuleFor(c => c).Must(c => c.Keyword is not null || c.Governorate is not null || c.City is not null || c.SalaryMin is not null
                || c.SalaryMax is not null || c.ContractType is not null || c.CategoryCode is not null)
            .WithErrorCode("VAL.Criteria.AtLeastOneRequired")
            .When(c => Enum.TryParse<InterestedReferenceType>(c.ReferenceType, true, out var t) && t == InterestedReferenceType.Filter);
    }
}

internal sealed class AddInterestedListEntryHandler : ICommandHandler<AddInterestedListEntryCommand, Guid>
{
    private readonly IInterestedListRepository _entries;
    private readonly IJobPostingRepository _postings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AddInterestedListEntryHandler(IInterestedListRepository entries, IJobPostingRepository postings, ICurrentUser user, TimeProvider clock)
    {
        _entries = entries;
        _postings = postings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(AddInterestedListEntryCommand request, CancellationToken ct)
    {
        var owner = _user.UserId!.Value;
        var reference = request.ToReference();
        if (reference.Type == InterestedReferenceType.Posting)
        {
            var posting = await _postings.GetByIdAsync(reference.PostingId!.Value, ct);
            if (posting is null)
            {
                return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
            }
        }

        var existing = await _entries.ListByOwnerAsync(owner, ct);
        var duplicate = existing.FirstOrDefault(e => e.Matches(reference));
        if (duplicate is not null)
        {
            return duplicate.Id;
        }

        var entry = InterestedListEntry.Add(owner, reference, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _entries.Add(entry);
        return entry.Id;
    }
}

internal sealed class DeleteInterestedListEntryHandler : ICommandHandler<DeleteInterestedListEntryCommand, Unit>
{
    private readonly IInterestedListRepository _entries;
    private readonly ICurrentUser _user;

    public DeleteInterestedListEntryHandler(IInterestedListRepository entries, ICurrentUser user)
    {
        _entries = entries;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteInterestedListEntryCommand request, CancellationToken ct)
    {
        var entry = await _entries.GetByIdAsync(request.InterestedListEntryId, ct);
        if (entry is null)
        {
            return Result.Success();
        }

        if (entry.OwnerAccountId != _user.UserId)
        {
            return Error.Forbidden(ErrorCodes.InterestedForbidden, "Only the owner may delete this entry.");
        }

        _entries.Remove(entry);
        return Result.Success();
    }
}

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
