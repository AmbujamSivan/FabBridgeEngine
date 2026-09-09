using Dashboard.Hubs;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Sinks;

/// <summary>
/// Broadcasts every translated <see cref="EquipmentStateChange"/> to all connected
/// dashboard clients over SignalR. This is the third sink — added alongside the console
/// and SQL sinks with, once again, zero changes to the TranslationWorker.
/// </summary>
public sealed class SignalRStateChangeSink : IStateChangeSink
{
    private readonly IHubContext<EquipmentHub> _hub;

    public SignalRStateChangeSink(IHubContext<EquipmentHub> hub) => _hub = hub;

    public async ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
    {
        var payload = new EquipmentStatusUpdate(
            EquipmentId: change.EquipmentId,
            State: change.State.ToString(),
            SourceCeid: (int)change.SourceCeid,
            Timestamp: change.Timestamp);

        await _hub.Clients.All.SendAsync(EquipmentHub.StateChangedEvent, payload, ct);
    }
}
