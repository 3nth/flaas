# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Fan-Light-as-a-Service (flaas) — a minimal ASP.NET Core Web API that controls a dimmable LED connected to a motherboard fan header via LibreHardwareMonitor. Designed to run as a Windows Service and integrate with Home Assistant as a REST-based light/switch.

## Build & Run

```bash
dotnet build
dotnet run                  # runs on http://localhost:5112
dotnet publish -c Release   # output to publish/
```

**Requires administrator privileges** — LibreHardwareMonitor needs elevated access to control fan hardware.

Target: .NET 9.0, win-x64 only.

## Install as Windows Service

```powershell
# From published output directory, run as admin:
.\install.ps1 -Account "NT AUTHORITY\LOCAL SERVICE"
```

This registers, starts, and smoke-tests the `flaas` Windows Service.

## Architecture

Two source files, no layers:

- **Program.cs** — Minimal API setup with five endpoints. Configures Windows Service hosting and AOT-compatible JSON serialization (`AppJsonSerializerContext`). Hardcoded to listen on `http://*:5112`.
- **FanLightController.cs** — Stateful singleton that wraps a LibreHardwareMonitor `ISensor`. Tracks `_isOn` and `_brightness` in memory. `CreateFanLightController` scans motherboard sub-hardware for a `SensorType.Control` sensor matching the configured name.

**Key types:** `State` (record: `IsOn`, `Brightness`), `UpdateVisitor` (LibreHardwareMonitor traversal helper).

## API Endpoints

| Method | Path          | Body                              | Description          |
|--------|---------------|-----------------------------------|----------------------|
| GET    | `/`           | —                                 | Current state        |
| POST   | `/`           | `{"isOn": bool, "brightness": N}` | Set full state       |
| POST   | `/on`         | —                                 | Turn on              |
| POST   | `/off`        | —                                 | Turn off             |
| POST   | `/brightness` | `{"brightness": N}`               | Set brightness (0–100) |

See `flaas.http` for example requests.

## Configuration

`appsettings.json` contains `SensorName` — the LibreHardwareMonitor sensor name (e.g., `"Fan #6"`). Use [Fan Control](https://getfancontrol.com/) to discover the correct name for your hardware.
