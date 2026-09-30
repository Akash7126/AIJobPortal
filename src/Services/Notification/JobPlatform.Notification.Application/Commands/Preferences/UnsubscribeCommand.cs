using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Preferences;

/// <summary>Anonymous one-click unsubscribe from the signed token in an e-mail (3.6.1-05 AC-02).</summary>
public sealed record UnsubscribeCommand(string Token) : ICommand<UnsubscribeResult>;
