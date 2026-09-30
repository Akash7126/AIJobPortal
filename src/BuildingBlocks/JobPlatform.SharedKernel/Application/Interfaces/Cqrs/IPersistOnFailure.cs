namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Command whose state changes (failed-attempt counters, lockouts) must be committed even when the result is a failure.</summary>
public interface IPersistOnFailure
{
}
