using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken ct);
}
