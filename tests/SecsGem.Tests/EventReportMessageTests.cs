using FabBridgeEngine.SecsGem;
using Secs4Net;
using Xunit;

namespace FabBridgeEngine.SecsGem.Tests;

public class EventReportMessageTests
{
    [Fact]
    public void BuildS6F11_Then_ReadCeid_RoundTrips()
    {
        var msg = EventReportMessage.BuildS6F11(ceid: 201);

        Assert.Equal((byte)6, msg.S);
        Assert.Equal((byte)11, msg.F);
        Assert.True(msg.ReplyExpected);                       // S6F11 expects S6F12 (W-bit)

        Assert.True(EventReportMessage.TryGetCeid(msg, out var ceid));
        Assert.Equal(201u, ceid);
    }

    [Fact]
    public void TryGetCeid_RejectsNonS6F11()
    {
        var notAnEvent = new SecsMessage(1, 13) { SecsItem = Item.A("hello") };
        Assert.False(EventReportMessage.TryGetCeid(notAnEvent, out _));
    }

    [Fact]
    public void TryGetCeid_RejectsMalformedS6F11Body()
    {
        // Right S/F, but the body isn't the expected list shape.
        var malformed = new SecsMessage(6, 11) { SecsItem = Item.A("oops") };
        Assert.False(EventReportMessage.TryGetCeid(malformed, out _));
    }

    [Fact]
    public void S6F12_IsSecondary_AndExpectsNoReply()
    {
        var ack = EventReportMessage.BuildS6F12();
        Assert.Equal((byte)6, ack.S);
        Assert.Equal((byte)12, ack.F);
        Assert.False(ack.ReplyExpected);
    }
}
