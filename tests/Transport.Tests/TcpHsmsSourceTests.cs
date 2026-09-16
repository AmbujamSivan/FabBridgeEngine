using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Transport;
using Xunit;

namespace FabBridgeEngine.Transport.Tests;

public class TcpHsmsSourceTests
{
    [Fact]
    public async Task Source_Receives_Events_Over_Tcp_Into_The_Channel()
    {
        // Full rung-3 loop: TcpEquipmentSimulator (server) → TcpHsmsSource (client) → channel.
        var sim = new TcpEquipmentSimulator(port: 0, equipmentIds: new[] { "ETCH-07" }, seed: 1);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = sim.RunAsync(cts.Token);

        var channel = new SecsEventChannel();
        var source = new TcpHsmsSource(channel, "127.0.0.1", sim.Port);
        var sourceTask = source.RunAsync(cts.Token);

        // Read a few events out of the channel — same consumer API the worker uses.
        var received = new List<SecsEvent>();
        await foreach (var evt in channel.ReadAllAsync(cts.Token))
        {
            received.Add(evt);
            if (received.Count >= 3) break;
        }

        Assert.True(received.Count >= 1);
        Assert.Equal(101u, received[0].Ceid);                 // lifecycle starts with Running
        Assert.All(received, e => Assert.Equal("ETCH-07", e.EquipmentId));

        cts.Cancel();
        await SwallowAsync(serverTask);
        await SwallowAsync(sourceTask);
    }

    [Fact]
    public async Task Source_Retries_When_No_Server_And_Stops_On_Cancel()
    {
        // Point at a port with nothing listening: the source must keep retrying, not throw.
        var channel = new SecsEventChannel();
        var logs = new List<string>();
        var source = new TcpHsmsSource(
            channel, "127.0.0.1", port: 1,   // port 1 → connection refused
            reconnectDelay: TimeSpan.FromMilliseconds(50),
            log: msg => { lock (logs) logs.Add(msg); });

        using var cts = new CancellationTokenSource();
        var task = source.RunAsync(cts.Token);

        await Task.Delay(300);   // let it fail-and-retry a few times
        Assert.False(task.IsFaulted, "source should not fault on connection failures");

        cts.Cancel();
        await SwallowAsync(task);

        Assert.True(task.IsCompleted);
        lock (logs) Assert.Contains(logs, m => m.Contains("failed") || m.Contains("retry"));
    }

    private static async Task SwallowAsync(Task t)
    {
        try { await t; } catch (OperationCanceledException) { }
    }
}
