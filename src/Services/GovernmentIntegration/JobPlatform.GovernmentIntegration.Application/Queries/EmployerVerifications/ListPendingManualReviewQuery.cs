using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.GovernmentIntegration.Application.Queries.EmployerVerifications;

public sealed record ListPendingManualReviewQuery(int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<EmployerVerificationView>>;
