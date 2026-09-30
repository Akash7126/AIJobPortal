using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

/// <param name="Kind">"Deactivate" or "Delete".</param>
public sealed record RequestAccountDeactivationCommand(Guid AccountId, string Kind, string Reason)
    : ServiceAuthorized, ICommand<DeactivationRequestResultDto>;
