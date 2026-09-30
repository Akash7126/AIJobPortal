namespace JobPlatform.SharedKernel.Application.Interfaces.Persistence;

/// <summary>Marker for aggregate repositories.</summary>
public interface IRepository<TAggregate, TId> where TAggregate : notnull where TId : notnull
{
}
