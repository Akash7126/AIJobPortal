using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;

public sealed record RevokeApiCredentialCommand(Guid ApiCredentialId) : ICommand<Unit>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string? RequiredPermission => Permissions.ApiCredentialsManage;
}
