using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Core.Translation;
using Xunit;

namespace FabBridgeEngine.Core.Tests;

public class CeidTranslatorTests
{
    private readonly CeidTranslator _translator = CeidTranslator.CreateDefault();

    // [Theory] runs the same test body once per [InlineData] row — a compact way to
    // assert many CEID→state mappings without repeating the test.
    [Theory]
    [InlineData(101u, EquipmentState.Running)]
    [InlineData(102u, EquipmentState.Idle)]
    [InlineData(201u, EquipmentState.Alarm)]
    [InlineData(301u, EquipmentState.Maintenance)]
    [InlineData(401u, EquipmentState.Down)]
    public void Translate_KnownCeid_MapsToExpectedState(uint ceid, EquipmentState expected)
    {
        var evt = SecsEvent.Create("ETCH-07", ceid);

        var change = _translator.Translate(evt);

        Assert.Equal(expected, change.State);
        Assert.Equal("ETCH-07", change.EquipmentId);
        Assert.Equal(ceid, change.SourceCeid); // traceability preserved
    }

    [Fact]
    public void Translate_UnknownCeid_ResolvesToUnknown_WithoutThrowing()
    {
        var evt = SecsEvent.Create("ETCH-07", ceid: 9999);

        var change = _translator.Translate(evt);

        Assert.Equal(EquipmentState.Unknown, change.State);
    }

    [Fact]
    public void Translate_PreservesEventTimestamp()
    {
        var when = new DateTimeOffset(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);
        var evt = new SecsEvent("ETCH-07", 101, when,
            new Dictionary<uint, object>());

        var change = _translator.Translate(evt);

        Assert.Equal(when, change.Timestamp);
    }
}
