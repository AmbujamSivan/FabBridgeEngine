using System.Text.Json;
using FabBridgeEngine.Core.Models;
using FabBridgeEngine.Messaging;
using Xunit;

namespace FabBridgeEngine.Messaging.Tests;

public class MqttMessageTests
{
    [Fact]
    public void BuildTopic_UsesPrefixAndEquipmentId()
    {
        Assert.Equal("fab/equipment/ETCH-07/state",
            MqttStateChangeSink.BuildTopic("fab/equipment", "ETCH-07"));
    }

    [Fact]
    public void BuildTopic_NormalizesTrailingSlash()
    {
        Assert.Equal("fab/equipment/CVD-03/state",
            MqttStateChangeSink.BuildTopic("fab/equipment/", "CVD-03"));
    }

    [Fact]
    public void BuildPayload_SerializesStateAsStringWithExpectedFields()
    {
        var when = new DateTimeOffset(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);
        var change = new EquipmentStateChange("ETCH-07", EquipmentState.Alarm, when, 201);

        var json = MqttStateChangeSink.BuildPayload(change);
        var back = JsonSerializer.Deserialize<MqttStatePayload>(json)!;

        Assert.Equal("ETCH-07", back.EquipmentId);
        Assert.Equal("Alarm", back.State);       // enum serialized as its name, not a number
        Assert.Equal(201, back.SourceCeid);
        Assert.Equal(when, back.Timestamp);
    }
}
