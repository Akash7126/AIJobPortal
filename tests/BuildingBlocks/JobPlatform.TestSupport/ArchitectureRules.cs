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

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private static IReadOnlyList<string> Names(TestResult result) => result.FailingTypeNames ?? Array.Empty<string>();
}
