using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;

/// <summary>Failed attempts are counted (aggregate and source), so state is persisted on failure.</summary>
public sealed record AuthenticateApiClientCommand(string GrantType, string ClientId, string ClientSecret)
    : ICommand<ClientTokenDto>, IPersistOnFailure;
