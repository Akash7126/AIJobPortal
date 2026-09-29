using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.Queries.Reference;
using JobPlatform.PlatformAdministration.Application.Reference;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Reference;

internal sealed class GetReferenceFileHandler : IQueryHandler<GetReferenceFileQuery, ReferenceFileView>
{
    private readonly ReferenceFileReader _reader;

    public GetReferenceFileHandler(ReferenceFileReader reader) => _reader = reader;

    public Task<Result<ReferenceFileView>> Handle(GetReferenceFileQuery request, CancellationToken ct) => _reader.ReadAsync(request.Type, false, ct);
}
