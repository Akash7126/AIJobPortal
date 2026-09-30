using JobPlatform.AccountIdentity.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Internal service clients (other BCs) that obtain tokens with client-credentials for /internal/v1.</summary>
public interface IServiceClientRegistry
{
    ServiceClient? Authenticate(string clientId, string clientSecret);
}
