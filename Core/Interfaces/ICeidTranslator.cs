using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Interfaces;

/// <summary>
/// The engine's "brain": converts a raw equipment <see cref="SecsEvent"/> (a CEID plus data)
/// into an MES-meaningful <see cref="EquipmentStateChange"/>.
///
/// This is intentionally a PURE, side-effect-free contract — no sockets, no database.
/// That makes the translation logic trivial to unit-test in isolation, and lets us swap
/// mapping strategies (hard-coded, config-driven, DB-driven) without touching any plumbing.
/// </summary>
public interface ICeidTranslator
{
    /// <summary>
    /// Maps a single collection event to a state transition.
    /// Unrecognized CEIDs resolve to <see cref="EquipmentState.Unknown"/> rather than throwing —
    /// a bridge must never crash on an event it doesn't recognize.
    /// </summary>
    EquipmentStateChange Translate(SecsEvent evt);
}
