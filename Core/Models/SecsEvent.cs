namespace FabBridgeEngine.Core.Models;

/// <summary>
/// Represents a single raw collection event received from equipment, as carried
/// by a SECS-II <c>S6F11</c> (Stream 6, Function 11 — "Event Report Send") message.
/// It mirrors an S6F11 message's structure, but is simplified to only the fields we care about.
/// This is the engine's INPUT: cryptic and equipment-centric. The Translation
/// Engine later maps <see cref="Ceid"/> into a meaningful MES state.
/// </summary>
/// <param name="EquipmentId">Logical id of the tool that emitted the event (e.g. "ETCH-07").</param>
/// <param name="Ceid">Collection Event ID — the number the machine fires when something happens.</param>
/// <param name="Timestamp">When the event occurred, as reported/received (UTC).</param>
/// <param name="ReportValues">
/// The data slots attached to the event: variable id (SVID/DVID/ECID) → value.
/// e.g. { 1001: 725.4 (chamber temp), 1002: "RecipeA" }.
/// </param>
public sealed record SecsEvent(
    string EquipmentId,
    uint Ceid,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<uint, object> ReportValues)
{
    /// <summary>Convenience: an event carrying no report variables.</summary>
    public static SecsEvent Create(string equipmentId, uint ceid) =>
        new(equipmentId, ceid, DateTimeOffset.UtcNow,
            new Dictionary<uint, object>());
}
