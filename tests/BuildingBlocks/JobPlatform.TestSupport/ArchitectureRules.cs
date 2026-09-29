using System.Reflection;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Domain;
using NetArchTest.Rules;

namespace JobPlatform.TestSupport;

/// <summary>
/// Structural rules of foundation section 4 / 14.1, reusable by every BC's ArchitectureTests project. Each method returns the names of
/// the offending types (empty = the rule holds) so a failing assertion names them.
/// </summary>
public static class ArchitectureRules
{
    private static readonly string[] FrameworkNamespaces =
    {
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FluentValidation", "RabbitMQ", "StackExchange.Redis"
    };

    public static IReadOnlyList<string> DomainDependsOn(Assembly domain, params string[] forbiddenNamespaces) =>
        Names(Types.InAssembly(domain).ShouldNot().HaveDependencyOnAny(forbiddenNamespaces.Concat(FrameworkNamespaces).ToArray()).GetResult());

    public static IReadOnlyList<string> DoesNotDependOn(Assembly assembly, params string[] forbiddenNamespaces) =>
        Names(Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbiddenNamespaces).GetResult());

    /// <summary>Other BCs must never be referenced: only SharedKernel and BuildingBlocks (and framework/packages) are allowed.</summary>
    public static IReadOnlyList<string> ReferencedOtherBoundedContexts(string bcName, params Assembly[] assemblies) =>
        assemblies.SelectMany(a => a.GetReferencedAssemblies())
            .Select(r => r.Name ?? string.Empty)
            .Where(n => n.StartsWith("JobPlatform.", StringComparison.Ordinal)
                        && !n.StartsWith("JobPlatform.SharedKernel", StringComparison.Ordinal)
                        && !n.StartsWith("JobPlatform.BuildingBlocks", StringComparison.Ordinal)
                        && !n.StartsWith($"JobPlatform.{bcName}.", StringComparison.Ordinal))
            .Distinct().ToArray();

    /// <summary>Request handlers are an implementation detail: internal sealed classes.</summary>
    public static IReadOnlyList<string> HandlersNotInternalSealed(Assembly application) =>
        application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .Where(t => t.IsPublic || !t.IsSealed)
            .Select(t => t.FullName!).ToArray();

    /// <summary>
    /// FluentValidation validators (and validator bases) live in the Application layer's Validators folder, namespace
    /// <c>&lt;Application&gt;.Validators.&lt;Feature&gt;</c>, never next to the commands or handlers they check.
    /// </summary>
    public static IReadOnlyList<string> ValidatorsOutsideValidatorsFolder(Assembly application)
    {
        var validatorsNs = application.GetName().Name + ".Validators";
        return application.GetTypes()
            .Where(t => t.IsClass && IsFluentValidator(t))
            .Where(t => t.Namespace is null || !(t.Namespace == validatorsNs || t.Namespace.StartsWith(validatorsNs + ".", StringComparison.Ordinal)))
            .Select(t => t.FullName!).ToArray();
    }

    /// <summary>
    /// CQRS layout of the Application layer: commands live in <c>&lt;Application&gt;.Commands.&lt;Feature&gt;</c>, queries in <c>.Queries.&lt;Feature&gt;</c>
    /// and request handlers in <c>.Handlers.&lt;Feature&gt;</c>, one handler class per request. Returns "type: reason" for each offender.
    /// </summary>
    public static IReadOnlyList<string> CqrsLayoutViolations(Assembly application)
    {
        var root = application.GetName().Name!;
        bool In(Type t, string folder) => t.Namespace is { } ns && ns.StartsWith($"{root}.{folder}.", StringComparison.Ordinal);
        var offenders = new List<string>();
        foreach (var type in application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } || t is { IsValueType: true, IsEnum: false }))
        {
            var interfaces = type.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition()).ToList();
            if (interfaces.Contains(typeof(IRequest<>)))
            {
                var folder = typeof(ICommandBase).IsAssignableFrom(type) ? "Commands" : "Queries";
                if (!In(type, folder)) offenders.Add($"{type.FullName}: request outside {folder}");
            }

            var handled = interfaces.Count(i => i == typeof(IRequestHandler<,>));
            if (handled > 0 && !In(type, "Handlers")) offenders.Add($"{type.FullName}: request handler outside Handlers");
            if (handled > 1) offenders.Add($"{type.FullName}: handles {handled} requests (one handler class per request)");
        }

        return offenders;
    }

    /// <summary>Aggregates are changed through behaviour methods only: no public setters on aggregate roots or their entities.</summary>
    public static IReadOnlyList<string> PublicSettersInDomain(Assembly domain) =>
        domain.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && IsEntityOrAggregate(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } && !IsInitOnly(p))
                .Select(p => $"{t.FullName}.{p.Name}")).ToArray();

    public static IReadOnlyList<string> ControllersUsingPersistence(Assembly api)
    {
        var controllerBase = typeof(Microsoft.AspNetCore.Mvc.ControllerBase);
        return api.GetTypes().Where(t => controllerBase.IsAssignableFrom(t))
            .Where(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Any(p =>
                typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(p.ParameterType)))
            .Select(t => t.FullName!).ToArray();
    }

    private static bool IsEntityOrAggregate(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFluentValidator(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition().FullName == "FluentValidation.AbstractValidator`1")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private static IReadOnlyList<string> Names(TestResult result) => result.FailingTypeNames ?? Array.Empty<string>();
}
