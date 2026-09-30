using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

public sealed record LogoutCommand : AuthenticatedRequest, ICommand<Unit>;
