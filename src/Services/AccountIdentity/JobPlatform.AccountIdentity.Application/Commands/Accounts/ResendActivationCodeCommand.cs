using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record ResendActivationCodeCommand(Guid AccountId) : ICommand<Unit>;
