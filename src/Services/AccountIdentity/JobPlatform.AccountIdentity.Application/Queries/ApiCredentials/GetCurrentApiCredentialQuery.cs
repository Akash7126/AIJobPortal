using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;

public sealed record GetCurrentApiCredentialQuery : IQuery<ApiCredentialView>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };
}
