using GameServer.Application;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Application.Tests;

public class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterWithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider);
    }
}
