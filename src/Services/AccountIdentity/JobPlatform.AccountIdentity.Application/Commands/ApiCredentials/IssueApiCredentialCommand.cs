using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;

/// <summary>IP whitelist, usage limit and expiration are independently optional; unset ones take platform defaults.</summary>
public sealed record IssueApiCredentialCommand(IReadOnlyList<string>? IpWhitelist, int? MaxRequests, int? PeriodSeconds, DateTime? ExpiresAtUtc)
    : ICommand<IssuedApiCredentialDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string? RequiredPermission => Permissions.ApiCredentialsManage;
}
