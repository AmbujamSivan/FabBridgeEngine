using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Translation;

/// <summary>
/// Dictionary-driven implementation of <see cref="ICeidTranslator"/>.
/// The CEID→state mapping is supplied as DATA, so new equipment/events can be
/// supported without modifying this class (Open/Closed Principle).
/// </summary>
public sealed class CeidTranslator : ICeidTranslator
{
    private readonly IReadOnlyDictionary<uint, EquipmentState> _map;

    public CeidTranslator(IReadOnlyDictionary<uint, EquipmentState> ceidToState)
    {
        // Defensive copy so the caller can't mutate our map after construction.
        _map = new Dictionary<uint, EquipmentState>(ceidToState);
    }

    /// <summary>
    /// A reasonable default mapping for our simulated tool. In a real fab this would
    /// come from configuration or a database, per equipment model.
    /// </summary>
    public static CeidTranslator CreateDefault() => new(new Dictionary<uint, EquipmentState>
    {
        [101] = EquipmentState.Running,     // process started
        [102] = EquipmentState.Idle,        // process completed
        [201] = EquipmentState.Alarm,       // alarm set
        [202] = EquipmentState.Idle,        // alarm cleared → back to idle
        [301] = EquipmentState.Maintenance, // entered maintenance
        [302] = EquipmentState.Idle,        // maintenance done
        [401] = EquipmentState.Down,        // comms/heartbeat lost
    });

    public EquipmentStateChange Translate(SecsEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Unknown CEID → Unknown state. A bridge must degrade gracefully, never throw,
        // on an event it doesn't recognize — the event is still logged for later analysis.
        var state = _map.TryGetValue(evt.Ceid, out var mapped)
            ? mapped
            : EquipmentState.Unknown;

        return new EquipmentStateChange(
            EquipmentId: evt.EquipmentId,
            State: state,
            Timestamp: evt.Timestamp,
            SourceCeid: evt.Ceid);
    }
}
