using GameServer.Application.Runtime;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Runtime boundary tests.
///
/// The Application-layer runtime service is a technical connection-lifecycle
/// boundary: it tracks who is connected and reports it. These tests confirm it
/// performs no gameplay work — it has no battle state, no Redis access, and no
/// domain rules.
/// </summary>
public class RuntimeServiceTests
{
    [Fact]
    public void OnConnected_ShouldReportConnectedStatus()
    {
        var runtime = new RuntimeService();

        var status = runtime.OnConnected("conn-1");

        Assert.Equal("conn-1", status.ConnectionId);
        Assert.Equal(RuntimeConnectionStatus.Connected, status.Status);
        Assert.Equal(1, runtime.ActiveConnectionCount);
    }

    [Fact]
    public void OnConnected_ShouldAssignMonotonicSequence()
    {
        var runtime = new RuntimeService();

        var first = runtime.OnConnected("conn-1");
        var second = runtime.OnConnected("conn-2");

        Assert.True(second.Sequence > first.Sequence);
    }

    [Fact]
    public void OnDisconnected_ShouldReportDisconnectedStatus()
    {
        var runtime = new RuntimeService();
        runtime.OnConnected("conn-1");

        var status = runtime.OnDisconnected("conn-1");

        Assert.NotNull(status);
        Assert.Equal(RuntimeConnectionStatus.Disconnected, status!.Status);
        Assert.Equal(0, runtime.ActiveConnectionCount);
    }

    [Fact]
    public void OnDisconnected_WithFault_ShouldReportFaultedStatus()
    {
        var runtime = new RuntimeService();
        runtime.OnConnected("conn-1");

        var status = runtime.OnDisconnected("conn-1", faulted: true);

        Assert.NotNull(status);
        Assert.Equal(RuntimeConnectionStatus.Faulted, status!.Status);
    }

    [Fact]
    public void OnDisconnected_ForUnknownConnection_ShouldReturnNull_AndNotThrow()
    {
        var runtime = new RuntimeService();

        var status = runtime.OnDisconnected("never-seen");

        Assert.Null(status);
    }

    [Fact]
    public void OnDisconnected_ShouldBeSafeWhenCalledTwice()
    {
        var runtime = new RuntimeService();
        runtime.OnConnected("conn-1");

        runtime.OnDisconnected("conn-1");
        var second = runtime.OnDisconnected("conn-1");

        Assert.Null(second);
        Assert.Equal(0, runtime.ActiveConnectionCount);
    }

    [Fact]
    public void GetStatus_ShouldReturnTrackedStatus_OrNullWhenUnknown()
    {
        var runtime = new RuntimeService();
        runtime.OnConnected("conn-1");

        Assert.NotNull(runtime.GetStatus("conn-1"));
        Assert.Null(runtime.GetStatus("unknown"));
    }

    [Fact]
    public void RuntimeStatus_ShouldCarryNoGameplayState()
    {
        // The runtime contract must expose only technical connection state.
        var properties = typeof(RuntimeStatus)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(
            new[] { "ConnectionId", "Status", "Sequence", "ObservedAt" },
            properties);
    }

    [Fact]
    public void OnConnected_ShouldRejectAnEmptyConnectionId()
    {
        var runtime = new RuntimeService();

        Assert.Throws<ArgumentException>(() => runtime.OnConnected(string.Empty));
    }
}