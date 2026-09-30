using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Taxonomy;

internal sealed class GetTaxonomyForConsumersHandler : IQueryHandler<GetTaxonomyForConsumersQuery, TaxonomyView>
{
    private readonly TaxonomyReader _reader;

    public GetTaxonomyForConsumersHandler(TaxonomyReader reader) => _reader = reader;

    public Task<Result<TaxonomyView>> Handle(GetTaxonomyForConsumersQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, request.Version, true, ct);
}
