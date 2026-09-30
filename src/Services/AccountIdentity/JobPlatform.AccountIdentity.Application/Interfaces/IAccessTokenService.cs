using JobPlatform.AccountIdentity.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>JWT issuing with asymmetric signing keys published through JWKS so every BC validates tokens locally.</summary>
public interface IAccessTokenService
{
    TimeSpan UserTokenLifetime { get; }

    IssuedAccessToken IssueUserToken(UserTokenRequest request);

    IssuedAccessToken IssueClientToken(ClientTokenRequest request);
}
