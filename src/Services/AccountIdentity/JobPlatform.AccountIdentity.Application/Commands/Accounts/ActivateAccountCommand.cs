using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

/// <summary>A wrong or expired code still consumes an attempt, so the aggregate is saved even when the result is a failure.</summary>
public sealed record ActivateAccountCommand(Guid AccountId, string Code) : ICommand<Unit>, IPersistOnFailure;
