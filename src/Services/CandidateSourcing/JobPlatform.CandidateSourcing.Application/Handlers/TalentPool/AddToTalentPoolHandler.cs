using JobPlatform.CandidateSourcing.Application.Commands.TalentPool;
using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.TalentPool;

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
