using System.Collections.Concurrent;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.BuildingBlocks.Infrastructure.Cqrs;

/// <summary>In-house CQRS dispatcher (foundation F-03, no third-party mediator): resolves the handler and wraps it in the registered pipeline behaviors.</summary>
internal sealed class Sender : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    private readonly IServiceProvider _services;

    public Sender(IServiceProvider services) => _services = services;

    public Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoker = (RequestInvoker<TResponse>)Invokers.GetOrAdd(request.GetType(), static (type, _) =>
            Activator.CreateInstance(typeof(RequestInvoker<,>).MakeGenericType(type, typeof(TResponse)))!, 0);
        return invoker.Invoke(request, _services, ct);
    }

    private abstract class RequestInvoker<TResponse>
    {
        public abstract Task<Result<TResponse>> Invoke(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class RequestInvoker<TRequest, TResponse> : RequestInvoker<TResponse> where TRequest : IRequest<TResponse>
    {
        public override Task<Result<TResponse>> Invoke(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToArray();

            RequestHandlerDelegate<TResponse> next = () => handler.Handle(typed, ct);
            foreach (var behavior in behaviors)
            {
                var inner = next;
                next = () => behavior.Handle(typed, inner, ct);
            }

            return next();
        }
    }
}
