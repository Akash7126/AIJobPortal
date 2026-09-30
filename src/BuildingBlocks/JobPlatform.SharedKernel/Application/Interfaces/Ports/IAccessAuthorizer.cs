using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

/// <summary>Decides whether the current user may execute a request (roles, permission, MFA) and records the decision.</summary>
public interface IAccessAuthorizer
{
    Task<Result<Unit>> AuthorizeAsync(ICurrentUser user, IAuthorizedRequest request, string requestName, CancellationToken ct = default);
}
