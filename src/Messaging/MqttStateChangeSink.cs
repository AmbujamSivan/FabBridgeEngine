using System.Text.Json;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace FabBridgeEngine.Messaging;

/// <summary>
/// Publishes every translated <see cref="EquipmentStateChange"/> to an MQTT broker — the
/// fourth <see cref="IStateChangeSink"/>, added (as ever) without touching the worker.
///
/// This realizes the "downstream pipeline" boundary: instead of the dashboard being the only
/// consumer, the bridge emits to a broker and ANY number of subscribers (MES, analytics, other
/// dashboards) receive the stream, decoupled from the bridge.
///
/// Messages are published RETAINED, one topic per tool. Retained means the broker keeps the last
/// message on each topic, so a subscriber that connects late immediately learns each tool's
/// current state — a standard IIoT "last known value" pattern.
/// </summary>
public sealed class MqttStateChangeSink : IStateChangeSink, IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _topicPrefix;
    private readonly Action<string>? _log;

    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MqttStateChangeSink(
        string host,
        int port = 1883,
        string topicPrefix = "fab/equipment",
        Action<string>? log = null)
    {
        _host = host;
        _port = port;
        _topicPrefix = topicPrefix.TrimEnd('/');
        _log = log;

        _client = new MqttFactory().CreateMqttClient();
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithClientId($"fabbridge-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();
    }

    // ── Pure, unit-testable message shaping ─────────────────────────────────────
    public static string BuildTopic(string topicPrefix, string equipmentId) =>
        $"{topicPrefix.TrimEnd('/')}/{equipmentId}/state";

    public static string BuildPayload(EquipmentStateChange change) =>
        JsonSerializer.Serialize(new MqttStatePayload(
            change.EquipmentId, 
            change.State.ToString(), 
            (int)change.SourceCeid, 
            change.Timestamp));

    public async ValueTask PublishAsync(EquipmentStateChange change, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(BuildTopic(_topicPrefix, change.EquipmentId))
            .WithPayload(BuildPayload(change))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)                       // keep last state per tool for late subscribers
            .Build();

        await _client.PublishAsync(message, ct);
    }

    /// <summary>Connect on first use; reconnect if the link dropped. Serialized so we connect once.</summary>
    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client.IsConnected) return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_client.IsConnected) return;          // double-check under the lock
            await _client.ConnectAsync(_options, ct);
            _log?.Invoke($"MQTT connected to {_host}:{_port}.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
        _gate.Dispose();
    }
}

/// <summary>JSON shape published to MQTT (stable property names for downstream consumers).</summary>
public sealed record MqttStatePayload(
    string EquipmentId,
    string State,
    int SourceCeid,
    DateTimeOffset Timestamp);
