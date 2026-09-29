using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Taxonomy;

internal sealed class GetPlatformTaxonomyHandler : IQueryHandler<GetPlatformTaxonomyQuery, TaxonomyView>
{
    private readonly TaxonomyReader _reader;

    public GetPlatformTaxonomyHandler(TaxonomyReader reader) => _reader = reader;

    public Task<Result<TaxonomyView>> Handle(GetPlatformTaxonomyQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, request.Version, false, ct);
}
