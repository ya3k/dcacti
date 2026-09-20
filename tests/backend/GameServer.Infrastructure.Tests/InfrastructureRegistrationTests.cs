using GameServer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Infrastructure.Tests;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructureServices_ShouldRegisterCleanly_WhenConnectionStringsEmpty()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }
}
