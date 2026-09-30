using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Delivery;

/// <summary>Daily digest (A-02-013): groups every pending digest-mode e-mail per recipient into one e-mail. Scheduled; a system command with no caller.</summary>
public sealed record BuildDailyDigestCommand(DateTime CutOffUtc) : ICommand<int>;
