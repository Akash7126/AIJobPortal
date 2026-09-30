namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Command that supports the Idempotency-Key header.</summary>
public interface IIdempotentCommand
{
    string? IdempotencyKey { get; }
}
