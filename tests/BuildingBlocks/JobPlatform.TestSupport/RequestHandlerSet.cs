using System.Reflection;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPlatform.TestSupport;

/// <summary>
/// Unit-test stand-in for the dispatcher (without the pipeline): builds the handler of an Application assembly that serves a request - one
/// handler class per request - from the given dependencies, and runs it. Constructor parameters are satisfied from the dependencies
/// (first assignable wins); a concrete Application class (an application service shared by several handlers) is built the same way,
/// and a missing <see cref="ILogger{T}"/> becomes a <see cref="NullLogger{T}"/>.
/// </summary>
public sealed class RequestHandlerSet
{
    private readonly Assembly _application;
    private readonly object[] _dependencies;

    public RequestHandlerSet(Assembly application, params object[] dependencies)
    {
        _application = application;
        _dependencies = dependencies;
    }

    /// <summary>Runs the handler registered for the request's runtime type.</summary>
    public Task<Result<TResponse>> Handle<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var handler = Create(typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse)));
        // A compiled delegate (not MethodInfo.Invoke) so exceptions thrown by the handler surface unwrapped, as they would from the dispatcher.
        var call = typeof(RequestHandlerSet).GetMethod(nameof(Call), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(request.GetType(), typeof(TResponse))
            .CreateDelegate<Func<object, object, CancellationToken, Task<Result<TResponse>>>>();
        return call(handler, request, ct);
    }

    private static Task<Result<TResponse>> Call<TRequest, TResponse>(object handler, object request, CancellationToken ct) where TRequest : IRequest<TResponse> =>
        ((IRequestHandler<TRequest, TResponse>)handler).Handle((TRequest)request, ct);

    /// <summary>The handler for <typeparamref name="TRequest"/>, typed as its request-handler contract.</summary>
    public IRequestHandler<TRequest, TResponse> For<TRequest, TResponse>() where TRequest : IRequest<TResponse> =>
        (IRequestHandler<TRequest, TResponse>)Create(typeof(IRequestHandler<TRequest, TResponse>));

    private object Create(Type contract)
    {
        var implementations = _application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(t)).ToList();
        if (implementations.Count != 1)
        {
            throw new InvalidOperationException($"Expected exactly one {Describe(contract)} in {_application.GetName().Name}, found {implementations.Count}.");
        }

        return Build(implementations[0]);
    }

    private object Build(Type type)
    {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(c => c.GetParameters().Length).FirstOrDefault()
                   ?? throw new InvalidOperationException($"{type.Name} has no public constructor.");
        var args = ctor.GetParameters().Select(p => Resolve(p.ParameterType, type)).ToArray();
        return ctor.Invoke(args);
    }

    private object Resolve(Type parameter, Type owner)
    {
        var supplied = _dependencies.FirstOrDefault(parameter.IsInstanceOfType);
        if (supplied is not null)
        {
            return supplied;
        }

        if (parameter is { IsClass: true, IsAbstract: false } && parameter.Assembly == _application)
        {
            return Build(parameter);
        }

        if (parameter.IsGenericType && parameter.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            return Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(parameter.GetGenericArguments()))!;
        }

        throw new InvalidOperationException($"No dependency supplied for {Describe(parameter)} required by {owner.Name}.");
    }

    private static string Describe(Type type) =>
        type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>" : type.Name;
}
