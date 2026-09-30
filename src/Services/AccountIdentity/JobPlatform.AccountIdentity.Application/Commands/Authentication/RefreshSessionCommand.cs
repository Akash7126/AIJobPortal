using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthenticationResultDto>;
