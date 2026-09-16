using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using Secs4Net;

namespace FabBridgeEngine.SecsGem;

/// <summary>
/// Rung-5 HOST side: an <see cref="IEquipmentSource"/> that speaks real HSMS/SECS-II via secs4net.
/// It connects (active mode) to a piece of equipment, receives genuine <c>S6F11</c> event reports,
/// extracts the CEID, writes a <see cref="SecsEvent"/> into the pipeline, and replies <c>S6F12</c>.
///
/// Like every other source, it's just an <see cref="IEquipmentSource"/> writing to an
/// <see cref="IEventProducer"/> — so the channel, worker, sinks, and dashboard are unchanged.
/// One HSMS connection == one tool, so the tool's identity comes from configuration
/// (<paramref name="equipmentId"/>), not the message body — exactly as in a real GEM host.
///
/// Reconnection is handled by secs4net itself (it transitions to <see cref="ConnectionState.Retry"/>
/// and re-establishes the link), so <see cref="GetPrimaryMessageAsync"/> keeps yielding across drops.
/// </summary>
public sealed class Secs4NetSource : IEquipmentSource
{
    private readonly IEventProducer _producer;
    private readonly string _host;
    private readonly int _port;
    private readonly ushort _deviceId;
    private readonly string _equipmentId;
    private readonly Action<string>? _log;

    public Secs4NetSource(
        IEventProducer producer, string host, int port,
        ushort deviceId, string equipmentId, Action<string>? log = null)
    {
        _producer = producer;
        _host = host;
        _port = port;
        _deviceId = deviceId;
        _equipmentId = equipmentId;
        _log = log;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var endpoint = SecsGemEndpoint.Create(new SecsGemOptions
        {
            IsActive = true,          // active = we connect to the equipment
            IpAddress = _host,
            Port = _port,
            DeviceId = _deviceId,
            T5 = 2000,                // shorten the connect-retry interval for responsiveness
        }, new NullSecsGemLogger());

        endpoint.Connection.ConnectionChanged += (_, state) =>
            _log?.Invoke($"HSMS host {_host}:{_port} → {state}");
        endpoint.Connection.Start(cancellationToken);

        try
        {
            await foreach (var wrapper in endpoint.Gem.GetPrimaryMessageAsync(cancellationToken))
            {
                using var primary = wrapper.PrimaryMessage;

                if (EventReportMessage.TryGetCeid(primary, out var ceid))
                {
                    var evt = new SecsEvent(_equipmentId, ceid, DateTimeOffset.UtcNow,
                        new Dictionary<uint, object>());
                    await _producer.WriteAsync(evt, cancellationToken);
                }

                // Acknowledge with S6F12 (the tool waits for this before sending the next event).
                await wrapper.TryReplyAsync(EventReportMessage.BuildS6F12(), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }
}
