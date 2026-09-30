using JobPlatform.BuildingBlocks.Api.Interfaces.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.TestSupport;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

/// <summary>Layout rules for the shared building blocks (each bounded context checks its own assemblies in its ArchitectureTests project).</summary>
public class BuildingBlockArchitectureTests
{
    [Fact]
    public void Interfaces_LiveInTheInterfacesFolderOfTheirLayer() =>
        ArchitectureRules.InterfacesOutsideInterfacesFolders(
            typeof(IDomainEvent).Assembly, typeof(ICacheStore).Assembly, typeof(IRemotePermissionChecker).Assembly).Should().BeEmpty();
}
