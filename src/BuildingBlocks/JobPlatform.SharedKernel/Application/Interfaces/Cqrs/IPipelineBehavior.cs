using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Cross-cutting step around a handler. First registered = outermost.</summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}
