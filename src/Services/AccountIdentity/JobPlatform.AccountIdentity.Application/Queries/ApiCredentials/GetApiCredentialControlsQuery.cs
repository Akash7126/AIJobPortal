using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;

public sealed record GetApiCredentialControlsQuery(Guid ApiCredentialId) : ServiceAuthorized, IQuery<ApiCredentialControlsDto>;
