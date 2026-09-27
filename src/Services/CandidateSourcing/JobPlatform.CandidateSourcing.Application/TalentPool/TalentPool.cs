using FluentValidation;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.TalentPool;

/// <summary>US-3.3.3-07: save a candidate to the employer's talent pool. Idempotent per (employer, candidate, posting) - AC-02.</summary>
public sealed record AddToTalentPoolCommand(Guid CandidateProfileId, Guid JobPostingId, string? Note) : EmployerCommand<TalentPoolEntryView>;

public sealed class AddToTalentPoolValidator : AbstractValidator<AddToTalentPoolCommand>
{
    public AddToTalentPoolValidator()
    {
        RuleFor(c => c.CandidateProfileId).NotEmpty().WithErrorCode("VAL.CandidateProfileId.Required");
        RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(c => c.Note).MaximumLength(500).WithErrorCode("VAL.Note.TooLong");
    }
}

internal sealed class AddToTalentPoolHandler : ICommandHandler<AddToTalentPoolCommand, TalentPoolEntryView>
{
    private readonly ITalentPoolEntryRepository _entries;
    private readonly ICandidateProjectionRepository _projections;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AddToTalentPoolHandler(ITalentPoolEntryRepository entries, ICandidateProjectionRepository projections, ICurrentUser user, TimeProvider clock)
    {
        _entries = entries;
        _projections = projections;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<TalentPoolEntryView>> Handle(AddToTalentPoolCommand request, CancellationToken ct)
    {
        var employerId = ActorFactory.From(_user).Id;
        var existing = await _entries.GetAsync(employerId, request.CandidateProfileId, request.JobPostingId, ct);
        if (existing is not null)
        {
            return ToView(existing);
        }

        var projection = await _projections.GetAsync(request.CandidateProfileId, ct);
        var snapshot = projection?.ToSnapshot(Array.Empty<string>()) ?? new CandidateSnapshot(request.CandidateProfileId, CandidateVisibility.Private, false, true, Array.Empty<string>());
        var visible = CandidatePrivacyPolicy.IsVisibleToEmployers(snapshot);

        var entry = Domain.TalentPool.TalentPoolEntry.Create(employerId, request.CandidateProfileId, request.JobPostingId, request.Note,
            _clock.GetUtcNow().UtcDateTime, employerId, visible);
        _entries.Add(entry);
        return ToView(entry);
    }

    private static TalentPoolEntryView ToView(Domain.TalentPool.TalentPoolEntry entry) =>
        new(entry.Id, entry.CandidateProfileId, entry.JobPostingId, entry.Note, entry.AddedAtUtc);
}

/// <summary>US-3.3.3-07: drop a saved candidate.</summary>
public sealed record RemoveFromTalentPoolCommand(Guid Id) : EmployerCommand<Unit>;

internal sealed class RemoveFromTalentPoolHandler : ICommandHandler<RemoveFromTalentPoolCommand, Unit>
{
    private readonly ITalentPoolEntryRepository _entries;
    private readonly ICurrentUser _user;

    public RemoveFromTalentPoolHandler(ITalentPoolEntryRepository entries, ICurrentUser user)
    {
        _entries = entries;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(RemoveFromTalentPoolCommand request, CancellationToken ct)
    {
        var entry = await _entries.GetByIdAsync(request.Id, ct);
        if (entry is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The talent-pool entry was not found.");
        }

        entry.Remove(ActorFactory.From(_user).Id);
        return Result.Success();
    }
}

public sealed record ListTalentPoolQuery : EmployerQuery<IReadOnlyList<TalentPoolEntryView>>;

internal sealed class ListTalentPoolHandler : IQueryHandler<ListTalentPoolQuery, IReadOnlyList<TalentPoolEntryView>>
{
    private readonly ICandidateSourcingReadStore _store;
    private readonly ICurrentUser _user;

    public ListTalentPoolHandler(ICandidateSourcingReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<TalentPoolEntryView>>> Handle(ListTalentPoolQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListTalentPoolAsync(ActorFactory.From(_user).Id, ct));
}
