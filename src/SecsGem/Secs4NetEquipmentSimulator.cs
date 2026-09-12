using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Core.Simulation;
using Secs4Net;

namespace FabBridgeEngine.SecsGem;

/// <summary>
/// Rung-5 EQUIPMENT side: a passive HSMS endpoint that behaves like a real tool. It waits for a
/// host to connect and select the link, then streams genuine <c>S6F11</c> event reports.
///
/// As with rungs 1 and 3, it does NOT re-implement the tool lifecycle — it hands the existing
/// <see cref="SimulatedEquipment"/> a producer (<see cref="SendingProducer"/>) that turns each
/// event into a real SECS message via <c>ISecsGem.SendAsync</c>. Same generator, now driving an
/// actual GEM link. (One tool per HSMS connection, so it simulates a single equipment id.)
/// </summary>
public sealed class Secs4NetEquipmentSimulator
{
    private readonly int _port;
    private readonly ushort _deviceId;
    private readonly string _equipmentId;
    private readonly int? _seed;
    private readonly Action<string>? _log;

    public Secs4NetEquipmentSimulator(
        int port, ushort deviceId = 0, string equipmentId = "ETCH-07",
        int? seed = null, Action<string>? log = null)
    {
        _port = port;
        _deviceId = deviceId;
        _equipmentId = equipmentId;
        _seed = seed;
        _log = log;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var endpoint = SecsGemEndpoint.Create(new SecsGemOptions
        {
            IsActive = false,          // passive = we listen for the host to connect
            IpAddress = "127.0.0.1",
            Port = _port,
            DeviceId = _deviceId,
        }, new NullSecsGemLogger());

        endpoint.Connection.ConnectionChanged += (_, state) =>
            _log?.Invoke($"HSMS equipment :{_port} → {state}");
        endpoint.Connection.Start(cancellationToken);

        // A real tool only reports events once the host has established (selected) the link.
        await endpoint.WaitUntilSelectedAsync(cancellationToken);
        _log?.Invoke("Host selected the link; equipment starting event stream.");

        // Reuse the rung-1 lifecycle generator, but each event goes out as a real S6F11.
        var producer = new SendingProducer(endpoint.Gem, _log);
        var equipment = new SimulatedEquipment(producer, new[] { _equipmentId }, _seed);
        await equipment.RunAsync(cancellationToken);
    }

    /// <summary>
    /// An <see cref="IEventProducer"/> that sends each event as an S6F11 and waits for the S6F12
    /// reply (<c>SendAsync</c> resolves when the secondary message arrives, honoring T3).
    /// </summary>
    private sealed class SendingProducer : IEventProducer
    {
        private readonly ISecsGem _gem;
        private readonly Action<string>? _log;

        public SendingProducer(ISecsGem gem, Action<string>? log)
        {
            _gem = gem;
            _log = log;
        }

        public async ValueTask WriteAsync(SecsEvent evt, CancellationToken cancellationToken = default)
        {
            try
            {
                await _gem.SendAsync(EventReportMessage.BuildS6F11(evt.Ceid), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;   // shutdown — let the lifecycle loop unwind
            }
            catch (Exception ex)
            {
                // Best-effort: a send failure (e.g. host briefly gone) shouldn't kill the tool.
                _log?.Invoke($"S6F11 send failed for CEID {evt.Ceid}: {ex.Message}");
            }
        }

        public void Complete() { }
    }
}
