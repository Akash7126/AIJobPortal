using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.EmployerOnboarding.Application.Queries.Registration;

public sealed record ListEmployerRegistrationsQuery(string? Status, int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<EmployerRegistrationView>>;
