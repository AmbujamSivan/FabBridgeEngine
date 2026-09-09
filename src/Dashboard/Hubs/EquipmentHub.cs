using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Hubs;

/// <summary>
/// Real-time hub that pushes equipment state changes to connected dashboard clients.
///
/// It has NO server-callable methods — this is a one-way, server→client broadcast hub.
/// The pipeline's <c>SignalRStateChangeSink</c> pushes via <see cref="IHubContext{EquipmentHub}"/>;
/// browsers subscribe and receive <c>"StateChanged"</c> messages.
/// </summary>
public sealed class EquipmentHub : Hub
{
    /// <summary>The client-side method name clients listen on.</summary>
    public const string StateChangedEvent = "StateChanged";

    /// <summary>The route the hub is mapped to.</summary>
    public const string Route = "/hubs/equipment";
}

/// <summary>
/// The wire payload broadcast to clients. A flat, JSON-friendly projection of
/// <c>EquipmentStateChange</c> (State as a string so the browser needn't know the enum).
/// </summary>
public sealed record EquipmentStatusUpdate(
    string EquipmentId,
    string State,
    int SourceCeid,
    DateTimeOffset Timestamp);
