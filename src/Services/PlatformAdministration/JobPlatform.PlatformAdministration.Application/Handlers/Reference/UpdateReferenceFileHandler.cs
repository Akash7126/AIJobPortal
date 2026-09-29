using JobPlatform.PlatformAdministration.Application.Commands.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.Reference;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Reference;

internal sealed class UpdateReferenceFileHandler : ICommandHandler<UpdateReferenceFileCommand, VersionResult>
{
    private readonly IReferenceFileRepository _files;
    private readonly IReferenceUsageChecker _usage;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateReferenceFileHandler(IReferenceFileRepository files, IReferenceUsageChecker usage, ICurrentUser user, TimeProvider clock)
    {
        _files = files;
        _usage = usage;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<VersionResult>> Handle(UpdateReferenceFileCommand request, CancellationToken ct)
    {
        ReferenceRules.TryParseType(request.Type, out var type);
        var file = await _files.GetByTypeAsync(type, ct);
        if (file is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The reference file was not found.");
        }

        var changes = request.Changes!.Select(Map).ToList();
        IReadOnlyCollection<string> inUse = Array.Empty<string>();
        var candidates = file.RemovalCandidateCodes(changes);
        if (candidates.Count > 0 && !request.ConfirmInUse)
        {
            var usage = await _usage.CheckAsync(type.ToString().ToLowerInvariant(), candidates, ct);
            // Fail-safe (handover 6.2): when the owners cannot answer, every removal needs explicit confirmation.
            inUse = usage.Available ? usage.InUse : candidates;
        }

        file.ApplyChanges(changes, request.ConfirmInUse, inUse, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return new VersionResult(file.FileVersion);
    }

    private static ReferenceChange Map(ReferenceChangeRequest r)
    {
        ReferenceRules.TryParseOp(r.Op, out var kind);
        return new ReferenceChange(kind, r.EntryId, r.Code, r.Name is null ? null : new LocalizedText(r.Name.Ar.Trim(), r.Name.En.Trim()), r.IsActive);
    }
}
