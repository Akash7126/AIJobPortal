using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Abstractions;

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();
