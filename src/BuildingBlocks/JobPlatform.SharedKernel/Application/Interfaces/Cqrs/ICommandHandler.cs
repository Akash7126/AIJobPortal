namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}
