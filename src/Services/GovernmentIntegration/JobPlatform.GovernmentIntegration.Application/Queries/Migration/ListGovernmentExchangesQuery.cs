using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Migration;

public sealed record ListGovernmentExchangesQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 50)
    : ServiceQuery<SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>;
