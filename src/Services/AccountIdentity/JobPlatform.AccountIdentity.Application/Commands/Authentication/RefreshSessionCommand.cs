using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthenticationResultDto>;
