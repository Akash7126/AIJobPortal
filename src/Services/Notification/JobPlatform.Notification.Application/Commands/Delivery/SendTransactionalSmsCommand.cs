using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Delivery;

/// <param name="Purpose">Otp or PasswordReset (both essential categories).</param>
public sealed record SendTransactionalSmsCommand(Guid AccountId, string Purpose, string Text, string? IdempotencyKey)
    : ServiceRequest, ICommand<TransactionalSmsResult>, IIdempotentCommand;
