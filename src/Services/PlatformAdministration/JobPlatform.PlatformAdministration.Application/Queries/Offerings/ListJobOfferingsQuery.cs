using JobPlatform.PlatformAdministration.Application.DTOs.Offerings;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.PlatformAdministration.Application.Queries.Offerings;

public sealed record ListJobOfferingsQuery(string? Status, int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<JobOfferingListItem>>;
