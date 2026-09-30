using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface ISender
{
    Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}
