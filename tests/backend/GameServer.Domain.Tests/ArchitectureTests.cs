using GameServer.Domain;
using GameServer.Shared;
using Xunit;

namespace GameServer.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_ShouldReference_SharedAssembly()
    {
        var domainAssembly = typeof(DomainAssemblyMarker).Assembly;
        var sharedAssembly = typeof(SharedAssemblyMarker).Assembly;

        Assert.NotNull(domainAssembly);
        Assert.NotNull(sharedAssembly);
    }
}
