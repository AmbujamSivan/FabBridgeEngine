using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Interfaces;

/// <summary>
/// A destination for translated <see cref="EquipmentStateChange"/> output.
///
/// This is the seam that keeps the translation worker ignorant of WHERE its output goes.
/// Later we'll have concrete sinks:
///   • SqlTelemetrySink   → persists to SQL Server (audit trail)
///   • SignalRSink        → broadcasts to the live Blazor dashboard
/// The worker just publishes to whatever sinks are registered — add or remove a
/// destination without ever touching the worker (Open/Closed again).
/// </summary>
public interface IStateChangeSink
{
    /// <summary>Handle one translated state change (persist it, broadcast it, etc.).</summary>
    ValueTask PublishAsync(EquipmentStateChange change, CancellationToken cancellationToken = default);
}
