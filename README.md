# FabBridgeEngine

**📊 [Live project overview →](https://ambujamsivan.github.io/FabBridgeEngine/)** — rendered architecture, the fidelity ladder, and design notes.

A SECS-II → MES bridge in C# / .NET 8. It ingests equipment collection events
(`S6F11` / `CEID`), translates them into MES operational states (`Running`, `Alarm`, …),
persists them to SQL Server, and (soon) streams them live to a Blazor dashboard.

See **[docs/architecture.md](docs/architecture.md)** for the full design.

## Structure

```
src/
  Core/          pure domain — models, interfaces, translation, channel, simulator (no deps)
  Persistence/   Dapper + SQL Server sink (sp_LogEquipmentTelemetry)
  Host/          console host that wires and runs the pipeline
tests/
  Core.Tests/    xUnit tests for the domain pipeline
db/              schema.sql, stored procedure, container runbook
docs/            architecture notes
```

## Quick start

**1. Start SQL Server** (see [db/README.md](db/README.md) for details):

```bash
docker run -d --name fabbridge-sql \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=FabBridge!2026" \
  -p 1433:1433 --platform linux/amd64 \
  mcr.microsoft.com/mssql/server:2022-latest
# then apply db/01_schema.sql and db/02_sp_LogEquipmentTelemetry.sql
```

**2. Build & test:**

```bash
dotnet build
dotnet test
```

**3. Run** (simulated equipment streams into the pipeline; Ctrl+C to stop):

```bash
dotnet run --project src/Host
```

Connection string defaults to the local Docker container; override with the
`FABBRIDGE_SQL` environment variable.

## Status

- [x] Core pipeline: models, translation, bounded channel, worker (Steps 1–4)
- [x] Simulated equipment + runnable console host (Step 5)
- [x] SQL Server persistence via Dapper + stored proc (Step 6)
- [ ] Blazor Server + SignalR live dashboard (Step 7)
- [ ] `feature/tcp-hsms` — real TCP/HSMS transport (rung 3)
- [ ] `feature/mqtt-pipeline` — broker-based pipeline (rung 4)
