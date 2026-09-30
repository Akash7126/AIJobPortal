using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface ICommand<TResponse> : IRequest<TResponse>, ICommandBase
{
}

public interface ICommand : ICommand<Unit>
{
}
