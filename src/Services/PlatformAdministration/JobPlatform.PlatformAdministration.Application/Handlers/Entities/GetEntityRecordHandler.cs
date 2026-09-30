using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Application.Queries.Entities;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Entities;

internal sealed class GetEntityRecordHandler : IQueryHandler<GetEntityRecordQuery, EntityRecordView>
{
    private readonly IAdminReadStore _store;

    public GetEntityRecordHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<EntityRecordView>> Handle(GetEntityRecordQuery request, CancellationToken ct) =>
        await _store.GetEntityRecordAsync(request.Id, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "The entity record was not found.");
}
