using System.Net;
using System.Net.Sockets;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Core.Simulation;

namespace FabBridgeEngine.Transport;

/// <summary>
/// Rung-3 EQUIPMENT side (the "tool"). Listens on a TCP port and, for each connected client,
/// streams HSMS-framed <c>S6F11</c> events.
///
/// Key reuse: it does NOT re-implement the tool lifecycle. It hands the existing
/// <see cref="SimulatedEquipment"/> a producer (<see cref="FrameWritingProducer"/>) that
/// encodes each event to a <see cref="HsmsFrame"/> and writes it to the socket. The
/// <see cref="IEventProducer"/> seam lets the very same event generator drive either an
/// in-memory channel (rung 1) or a network socket (rung 3).
/// </summary>
public sealed class TcpEquipmentSimulator
{
    private readonly TcpListener _listener;
    private readonly IReadOnlyList<string> _equipmentIds;
    private readonly int? _seed;

    /// <summary>The bound port (useful when constructed with port 0 for an OS-assigned port).</summary>
    public int Port { get; }

    public TcpEquipmentSimulator(int port = 5555, IEnumerable<string>? equipmentIds = null, int? seed = null)
    {
        _equipmentIds = (equipmentIds ?? new[] { "ETCH-07", "CVD-03", "LITHO-11" }).ToArray();
        _seed = seed;

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await ServeClientAsync(client, cancellationToken);   // serve one bridge at a time
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            var producer = new FrameWritingProducer(stream);
            var equipment = new SimulatedEquipment(producer, _equipmentIds, _seed);
            try
            {
                await equipment.RunAsync(ct);   // streams frames until cancel or disconnect
            }
            catch (IOException) { /* client dropped the connection — accept the next one */ }
            catch (OperationCanceledException) { throw; }
        }
    }

    /// <summary>
    /// An <see cref="IEventProducer"/> that serializes each event to an <see cref="HsmsFrame"/>
    /// and writes it to the network stream.
    ///
    /// The <see cref="SemaphoreSlim"/> is essential: <see cref="SimulatedEquipment"/> runs one
    /// loop PER tool concurrently, so several tasks call <see cref="WriteAsync"/> at once. Writing
    /// to a single socket from multiple tasks without serialization would interleave bytes and
    /// corrupt frames. We lock so each frame is written atomically — the same reason the channel
    /// declared SingleWriter.
    /// </summary>
    private sealed class FrameWritingProducer : IEventProducer
    {
        private readonly Stream _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public FrameWritingProducer(Stream stream) => _stream = stream;

        public async ValueTask WriteAsync(SecsEvent evt, CancellationToken cancellationToken = default)
        {
            var frame = HsmsFrame.Encode(evt);
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _stream.WriteAsync(frame, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Complete() { /* nothing to flush; stream is disposed by the caller */ }
    }
}
