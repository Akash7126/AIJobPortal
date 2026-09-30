namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
