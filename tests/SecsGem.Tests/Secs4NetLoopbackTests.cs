using System.Net;
using System.Net.Sockets;
using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.SecsGem;
using Xunit;
using Xunit.Abstractions;

namespace FabBridgeEngine.SecsGem.Tests;

public class Secs4NetLoopbackTests
{
    private readonly ITestOutputHelper _out;
    public Secs4NetLoopbackTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Host_Receives_Real_S6F11_Events_From_Equipment_Over_Hsms()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Equipment (passive) — waits for the host, then streams real S6F11s.
        var equipment = new Secs4NetEquipmentSimulator(
            port, deviceId: 0, equipmentId: "ETCH-07", seed: 1,
            log: m => _out.WriteLine($"[equip] {m}"));
        var equipTask = equipment.RunAsync(cts.Token);

        // Host (active) — connects, decodes S6F11 → SecsEvent into the channel, replies S6F12.
        var channel = new SecsEventChannel();
        var source = new Secs4NetSource(
            channel, "127.0.0.1", port, deviceId: 0, equipmentId: "ETCH-07",
            log: m => _out.WriteLine($"[host]  {m}"));
        var sourceTask = source.RunAsync(cts.Token);

        // Pull the first event that made the full round trip over HSMS.
        var received = new List<SecsEvent>();
        await foreach (var evt in channel.ReadAllAsync(cts.Token))
        {
            received.Add(evt);
            if (received.Count >= 1) break;
        }

        Assert.NotEmpty(received);
        Assert.Equal(101u, received[0].Ceid);              // lifecycle starts with process-start
        Assert.Equal("ETCH-07", received[0].EquipmentId);  // identity comes from host config

        cts.Cancel();
        await Swallow(equipTask);
        await Swallow(sourceTask);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task Swallow(Task t)
    {
        try { await t; } catch (OperationCanceledException) { }
    }
}
