# FabBridgeEngine — Architecture

A production-grade, microservice-style bridge (C# / .NET 8, Dapper/T-SQL, Blazor Server)
that ingests SECS-II equipment events, translates them into MES operational states,
persists them, and streams them live to a dashboard.

## Flow

```
                  ┌─────────────────────────────────────────┐
                  │   Factory Equipment / OPC UA Server      │
                  └────────────────────┬────────────────────┘
                                       │ SECS-II (HSMS) / OPC UA
                                       ▼
                  ┌─────────────────────────────────────────┐
                  │       Equipment Bridge Service           │
                  │   (SECS/GEM SDK + OPC UA Client)         │
                  └────────────────────┬────────────────────┘
                                       │ Raw Event Payload (CEID)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     High-Throughput Ingestion Engine                     │
│   ┌────────────────────┐      Channel       ┌───────────────────────┐    │
│   │  S6F11 Handler     ├───────────────────►│ Translation Engine    │    │
│   │ (SVID/DVID/ECID)   │                    │ (CEID -> MES State)   │    │
│   └────────────────────┘                    └───────────┬───────────┘    │
└─────────────────────────────────────────────────────────┼───────────────┘
                                         ┌─────────────────┴──────────────┐
                                         ▼                                ▼
                         ┌─────────────────────────┐    ┌───────────────────┐
                         │   SQL Server (T-SQL)     │    │ SignalR Hub       │
                         │   Telemetry & Audit      │    └─────────┬─────────┘
                         └─────────────────────────┘              ▼
                                                        ┌───────────────────┐
                                                        │ Blazor Live       │
                                                        │ Status Dashboard  │
                                                        └───────────────────┘
```

## The core transformation

Everything reduces to one function: **`SecsEvent → EquipmentStateChange`**.
The rest (channel, SQL, SignalR, Blazor) is plumbing around it.

- **`SecsEvent`** — raw INPUT. A `CEID` plus a bag of variable values (SVID/DVID/ECID).
- **`EquipmentStateChange`** — MES OUTPUT. `Running`, `Idle`, `Alarm`, `Maintenance`, `Down`.

## Default CEID → state mapping (simulated tool)

| CEID | Meaning              | MES State     |
|------|----------------------|---------------|
| 101  | process started      | `Running`     |
| 102  | process completed    | `Idle`        |
| 201  | alarm set            | `Alarm`       |
| 202  | alarm cleared        | `Idle`        |
| 301  | entered maintenance  | `Maintenance` |
| 302  | maintenance done     | `Idle`        |
| 401  | comms/heartbeat lost | `Down`        |
| *other* | unrecognized      | `Unknown`     |

Mapping is **data, not code** (`CeidTranslator` takes a dictionary), so new tools/events
are a config change, not a code change.

## Key design decisions

- **Bounded `System.Threading.Channels`** decouples a fast/bursty receiver from a steady
  translator. Bounded (not unbounded) → backpressure instead of unbounded memory growth.
- **`IStateChangeSink` fan-out** — translated output goes to N sinks (console, SQL, SignalR)
  with per-sink fault isolation. Adding a destination never touches the worker.
- **`Core` has zero external dependencies** — SQL/Dapper live in `Persistence`, web/SignalR
  in `Dashboard`. That keeps the domain logic fast and trivially unit-testable.
- **Stored procedure writes** (`sp_LogEquipmentTelemetry` via Dapper) for cached plans.

## Producer fidelity ladder (how "real" the equipment side is)

| Rung | Approach                         | Exercises                                  |
|------|----------------------------------|--------------------------------------------|
| 1    | In-process `SimulatedEquipment`  | pipeline logic only                        |
| 2    | Emit/parse real SECS-II bytes    | + binary framing/parsing                   |
| 3    | TCP loopback (`TcpHsmsSource`)   | + sockets, HSMS handshake, T3–T8 timers    |
| 4    | Message broker (MQTT/Kafka)      | + durable pipeline, at-least-once delivery |
| 5    | Real SECS/GEM stack (secs4net)   | + full protocol, GEM state machine         |

All rungs end at `producer.WriteAsync(evt)`, so the downstream pipeline is identical
regardless of rung. `IEquipmentSource` is the swap seam.

Branch plan: `main` (rungs 1–2) → `feature/tcp-hsms` (rung 3) → `feature/mqtt-pipeline` (rung 4).
