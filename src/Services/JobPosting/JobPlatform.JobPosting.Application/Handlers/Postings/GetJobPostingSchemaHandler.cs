using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

internal sealed class GetJobPostingSchemaHandler : IQueryHandler<GetJobPostingSchemaQuery, JobPostingSchemaView>
{
    private readonly IJobPostingSearchReadModel _search;

    public GetJobPostingSchemaHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<JobPostingSchemaView>> Handle(GetJobPostingSchemaQuery request, CancellationToken ct) => await _search.GetSchemaAsync(ct);
}
