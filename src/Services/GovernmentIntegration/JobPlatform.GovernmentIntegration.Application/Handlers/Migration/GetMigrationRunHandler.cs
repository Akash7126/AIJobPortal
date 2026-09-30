using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Application.Queries.Migration;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class GetMigrationRunHandler : IQueryHandler<GetMigrationRunQuery, MigrationRunView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetMigrationRunHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<MigrationRunView>> Handle(GetMigrationRunQuery request, CancellationToken ct) =>
        await _store.GetMigrationRunAsync(request.MigrationRunId, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
}
