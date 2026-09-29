using JobPlatform.JobPosting.Application.Queries.InternalApi;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.InternalApi;

internal sealed class CheckReferenceUsageHandler : IQueryHandler<CheckReferenceUsageQuery, IReadOnlyList<string>>
{
    private readonly IJobPostingSearchReadModel _search;

    public CheckReferenceUsageHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<IReadOnlyList<string>>> Handle(CheckReferenceUsageQuery request, CancellationToken ct) =>
        Result.Success(await _search.CheckReferenceUsageAsync(request.Type, request.Codes, ct));
}
