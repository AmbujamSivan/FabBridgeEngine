namespace FabBridgeEngine.Core.Models;

/// <summary>
/// The MES-meaningful operational states a piece of equipment can be in.
/// The Translation Engine converts raw <see cref="SecsEvent.Ceid"/> values into one of these.
/// </summary>
public enum EquipmentState
{
    /// <summary>State not yet known (e.g. just after startup, before first event).</summary>
    Unknown = 0,

    /// <summary>Powered and available, but not actively processing material.</summary>
    Idle,

    /// <summary>Actively processing a wafer/lot.</summary>
    Running,

    /// <summary>An alarm condition is active; requires attention.</summary>
    Alarm,

    /// <summary>Taken offline deliberately for scheduled/unscheduled maintenance.</summary>
    Maintenance,

    /// <summary>Unexpectedly offline / not communicating.</summary>
    Down
}

/// <summary>
/// A translated state transition — the engine's OUTPUT. This is what gets persisted
/// to SQL for audit and streamed to the Blazor dashboard via SignalR.
/// </summary>
/// <param name="EquipmentId">Which tool changed state.</param>
/// <param name="State">The new MES state.</param>
/// <param name="Timestamp">When the originating event occurred (UTC).</param>
/// <param name="SourceCeid">The CEID that triggered this transition (traceability).</param>

// used record (not class) — these are immutable data-carriers, and value equality is handy for testing/comparison.
// Perfect fit for events. sealed — no inheritance intended here, and avoids accidental slicing if someone tries to derive from it.;
public sealed record EquipmentStateChange(
    string EquipmentId,
    EquipmentState State,
    DateTimeOffset Timestamp,
    uint SourceCeid);
