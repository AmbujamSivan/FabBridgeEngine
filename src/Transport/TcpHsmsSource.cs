using System.Net.Sockets;
using FabBridgeEngine.Core.Interfaces;

namespace FabBridgeEngine.Transport;

/// <summary>
/// Rung-3 HOST side (the bridge's receiver). An <see cref="IEquipmentSource"/> that connects
/// to equipment over TCP, decodes HSMS frames into <see cref="Core.Models.SecsEvent"/>s, and
/// writes them into the pipeline's channel.
///
/// It is a straight swap for <c>SimulatedEquipment</c>: both are <see cref="IEquipmentSource"/>s
/// that pump events into an <see cref="IEventProducer"/>. Nothing downstream (channel, worker,
/// sinks, dashboard) knows or cares that the events now arrive over a socket.
///
/// Resilience: if the connection can't be made or drops, it waits <see cref="_reconnectDelay"/>
/// and retries — indefinitely — until cancelled. Losing the link is normal in a fab (a tool
/// reboots, a switch blips); the bridge must reconnect on its own.
/// </summary>
public sealed class TcpHsmsSource : IEquipmentSource
{
    private readonly IEventProducer _producer;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _reconnectDelay;
    private readonly Action<string>? _log;

    public TcpHsmsSource(
        IEventProducer producer,
        string host,
        int port,
        TimeSpan? reconnectDelay = null,
        Action<string>? log = null)
    {
        _producer = producer;
        _host = host;
        _port = port;
        _reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(2);
        _log = log;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReceiveAsync(cancellationToken);
                // Returning here means the peer closed cleanly — reconnect after a pause.
                _log?.Invoke($"Connection to {_host}:{_port} closed; will reconnect.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // genuine shutdown
            }
            catch (Exception ex)
            {
                // Connect failed or the stream faulted mid-flight. Log and retry.
                _log?.Invoke($"Connection to {_host}:{_port} failed: {ex.Message}; retrying in {_reconnectDelay.TotalSeconds:0.#}s.");
            }

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ConnectAndReceiveAsync(CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, ct);
        _log?.Invoke($"Connected to equipment at {_host}:{_port}.");

        await using var stream = client.GetStream();
        var reader = new HsmsFrameReader(stream);

        // Decode each frame and hand it to the pipeline. The channel's backpressure naturally
        // throttles us here if the translator falls behind — exactly as in rung 1.
        await foreach (var evt in reader.ReadAllAsync(ct))
        {
            await _producer.WriteAsync(evt, ct);
        }
    }
}
