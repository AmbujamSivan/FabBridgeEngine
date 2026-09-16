using System.Net;
using System.Net.Sockets;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Transport;
using Xunit;

namespace FabBridgeEngine.Transport.Tests;

public class TcpEquipmentSimulatorTests
{
    [Fact]
    public async Task Client_Receives_Framed_Events_Over_Tcp()
    {
        // One tool, port 0 → OS-assigned port. Seed keeps the first emitted CEID deterministic.
        var sim = new TcpEquipmentSimulator(port: 0, equipmentIds: new[] { "ETCH-07" }, seed: 1);
        using var serverCts = new CancellationTokenSource();
        var serverTask = sim.RunAsync(serverCts.Token);

        // Connect as a client and read the first few frames back through the real codec.
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, sim.Port);

        var reader = new HsmsFrameReader(client.GetStream());
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // safety timeout

        var received = new List<SecsEvent>();
        await foreach (var evt in reader.ReadAllAsync(readCts.Token))
        {
            received.Add(evt);
            if (received.Count >= 3) break;
        }

        // The lifecycle always begins with CEID 101 (process start → Running).
        Assert.True(received.Count >= 1);
        Assert.Equal(101u, received[0].Ceid);
        Assert.All(received, e => Assert.Equal("ETCH-07", e.EquipmentId));

        serverCts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }
}
