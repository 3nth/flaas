# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Fan-Light-as-a-Service (flaas) — a minimal ASP.NET Core Web API that controls a dimmable LED connected to a motherboard fan header via LibreHardwareMonitor. Designed to run as a Windows Service and integrate with Home Assistant via MQTT Discovery.

## Build & Run

```bash
dotnet build
dotnet run                  # runs on http://localhost:5112
dotnet publish -c Release -o C:\flaas  # deploy to service directory
```

**Requires administrator privileges** — LibreHardwareMonitor needs elevated access to control fan hardware.

**Note:** `appsettings.json` is excluded from publish output to avoid overwriting production config.

Target: .NET 10.0, win-x64 only.

## Install as Windows Service

```powershell
# From the publish directory, run as admin:
.\install.ps1
```

Defaults to LocalSystem (needs admin for LibreHardwareMonitor). Override with `-Account` if needed. Manage with `sc start flaas` / `sc stop flaas`.

## Architecture

Three source files, no layers:

- **Program.cs** — Minimal API setup with six endpoints (including `/health`). Configures Windows Service hosting, AOT-compatible JSON serialization (`AppJsonSerializerContext`), and optional API key middleware. Listens on configurable URL (default `http://*:5112`).
- **FanLightController.cs** — Stateful singleton that wraps a LibreHardwareMonitor `ISensor`. Tracks `_isOn` and `_brightness` in memory. Fires `StateChanged` event on every mutation. `CreateFanLightController` scans motherboard sub-hardware for a `SensorType.Control` sensor matching the configured name.
- **MqttBridge.cs** — `BackgroundService` that connects to an MQTT broker (with optional TLS) and publishes HA MQTT Discovery config (default schema with separate state/brightness topics). Subscribes to command topics for on/off and brightness. Listens to `homeassistant/status` birth message to re-announce on HA restart. Publishes availability via LWT. Exposes `MqttEnabled`/`MqttConnected` for the `/health` endpoint.

**Key types:** `State` (record: `IsOn`, `Brightness`), `Health` (record: `MqttEnabled`, `MqttConnected`), `UpdateVisitor` (LibreHardwareMonitor traversal helper).

## API Endpoints

| Method | Path          | Body                              | Description          |
|--------|---------------|-----------------------------------|----------------------|
| GET    | `/health`     | —                                 | Health check (200 OK)|
| GET    | `/`           | —                                 | Current state        |
| POST   | `/`           | `{"isOn": bool, "brightness": N}` | Set full state       |
| POST   | `/on`         | —                                 | Turn on              |
| POST   | `/off`        | —                                 | Turn off             |
| POST   | `/brightness` | `{"brightness": N}`               | Set brightness (0–100) |

See `flaas.http` for example requests.

## Configuration

`appsettings.json` contains:

- `SensorName` — the LibreHardwareMonitor sensor name (e.g., `"AIO Pump"`). Use `--list-sensors` or [Fan Control](https://getfancontrol.com/) to discover the correct name.
- `ListenUrl` — bind address (default `http://*:5112`). Set to `http://localhost:5112` to restrict to loopback.
- `ApiKey` — when set, POST endpoints require `X-Api-Key` header. GET endpoints remain open.
- `Mqtt` section — broker connection and HA discovery settings. Leave `Host` empty to disable MQTT. Set `Tls` to `true` for encrypted connections (port defaults to 8883).

The service runs from `C:\flaas`. The source repo is at `D:\flaas`. Always use `pwsh` for shell commands.
