using Secs4Net;
using static Secs4Net.Item;

namespace FabBridgeEngine.SecsGem;

/// <summary>
/// Builds and reads a real SECS-II <c>S6F11</c> ("Event Report Send") using secs4net's
/// typed <see cref="Item"/> tree — the genuine article, not our hand-rolled rung-3 frame.
///
/// The S6F11 body follows the SEMI E5 structure:
///   L[3]
///     ├─ U4  DATAID      (report data id; 0 here)
///     ├─ U4  CEID        (the collection event id)
///     └─ L[n] reports    (each L[2]{ RPTID, L[values] } — empty in our simplified model)
///
/// We keep the CEID (all our translator needs) and leave the report list empty; populating it
/// would require the S2F33/S2F35 define/link handshake, which is beyond this rung.
/// </summary>
public static class EventReportMessage
{
    public const byte Stream = 6;
    public const byte Function = 11;

    /// <summary>Build an S6F11 carrying <paramref name="ceid"/>. Reply (S6F12) is expected (W-bit).</summary>
    public static SecsMessage BuildS6F11(uint ceid, uint dataId = 0) =>
        new(Stream, Function, replyExpected: true)
        {
            SecsItem = L(
                U4(dataId),   // DATAID
                U4(ceid),     // CEID
                L())          // report list (empty)
        };

    /// <summary>Build the S6F12 acknowledgement (secondary message; no further reply).</summary>
    public static SecsMessage BuildS6F12() =>
        new(Stream, (byte)(Function + 1), replyExpected: false)
        {
            SecsItem = B(0),  // ACKC6 = 0 (accepted)
        };

    /// <summary>
    /// Extract the CEID from a message if it is a well-formed S6F11. Returns false (never throws)
    /// for anything else — a receiver must degrade gracefully on unexpected messages.
    /// </summary>
    public static bool TryGetCeid(SecsMessage message, out uint ceid)
    {
        ceid = 0;
        if (message.S != Stream || message.F != Function) return false;

        var body = message.SecsItem;
        if (body is null) return false;

        try
        {
            if (body.Count < 2) return false;      // need at least DATAID + CEID
            ceid = body[1].FirstValue<uint>();     // second item is the CEID
            return true;
        }
        catch
        {
            return false;   // body wasn't the shape we expected
        }
    }
}
