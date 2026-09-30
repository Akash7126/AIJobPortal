namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

public interface ICorrelationContext
{
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
}
