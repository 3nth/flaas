# Fan-Light-as-a-Service

A minimal ASP.NET Core Web API that controls a dimmable LED connected to a motherboard fan header via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Runs as a Windows Service and integrates with [Home Assistant](https://www.home-assistant.io/) via MQTT Discovery.

## Setup

1. Connect a dimmable LED to a fan header on your motherboard.

2. Identify the sensor name using [Fan Control](https://getfancontrol.com/), or run:
   ```bash
   dotnet run -- --list-sensors
   ```

3. Set the sensor name in `appsettings.json`:
   ```json
   {
     "SensorName": "AIO Pump"
   }
   ```

4. Run (requires administrator privileges for hardware control):
   ```bash
   dotnet run
   ```

## Home Assistant Integration

flaas uses MQTT with Home Assistant Discovery. When configured, the light entity appears automatically in Home Assistant -- no manual HA configuration needed.

Add the MQTT section to `appsettings.json`:

```json
{
  "SensorName": "AIO Pump",
  "HardwareMin": 55,
  "HardwareMax": 100,
  "Mqtt": {
    "Host": "your-mqtt-broker",
    "Port": 1883,
    "Tls": false,
    "Username": "",
    "Password": "",
    "TopicPrefix": "flaas",
    "DeviceName": "Fan Light"
  }
}
```

The MQTT bridge will:
- Publish a discovery config so HA auto-creates a dimmable light entity
- Publish state and brightness changes in real time
- Accept on/off and brightness commands from HA
- Re-announce when HA restarts (via the `homeassistant/status` birth message)
- Report availability (online/offline) including on unexpected disconnects via MQTT LWT

`HardwareMin` and `HardwareMax` map the 1-100% brightness range to your hardware's actual usable range. For example, if your LED is off below 55%, set `HardwareMin` to 55. Both are optional and default to 1/100.

Set `Mqtt:Tls` to `true` to connect via TLS (port defaults to 8883). Works with the HA Mosquitto add-on's TLS listener and Let's Encrypt certs.

Leave `Mqtt:Host` empty to disable MQTT and use only the REST API.

## Security

**API key** — Set `ApiKey` in `appsettings.json` to require an `X-Api-Key` header on all POST requests. GET endpoints (state, UI, health) remain open. Access the UI with the key via `/ui?key=YOUR_KEY`.

**Listen address** — Set `ListenUrl` to control the bind address (default `http://*:5112`). Use `http://localhost:5112` to restrict to loopback when only MQTT control is needed.

**MQTT TLS** — Set `Mqtt:Tls` to `true` to encrypt the broker connection. Port defaults to 8883 when TLS is enabled.

## Build & Deploy

```bash
dotnet build
dotnet run                          # runs on http://localhost:5112 (configurable via ListenUrl)
dotnet publish -c Release -o C:\flaas  # deploy to service directory
```

**Note:** `appsettings.json` is excluded from publish output to avoid overwriting production config. Target: .NET 10.0, win-x64 only.

## Install as Windows Service

```powershell
# From the publish directory, run as admin:
.\install.ps1
```

The installer will prompt you to select a sensor if `SensorName` is not already configured. 

The service account defaults to LocalSystem since the service needs admin access to control fan hardware via LibreHardwareMonitor. Override with `-Account` if needed.

Manage the service with:
```
sc start flaas
sc stop flaas
```

## REST API

A web UI is available at `/ui`. The following endpoints are also available:

| Method | Path          | Body                              | Description            |
|--------|---------------|-----------------------------------|------------------------|
| GET    | `/health`     | --                                | Health check (200 OK)  |
| GET    | `/`           | --                                | Current state          |
| POST   | `/`           | `{"isOn": bool, "brightness": N}` | Set full state         |
| POST   | `/on`         | --                                | Turn on                |
| POST   | `/off`        | --                                | Turn off               |
| POST   | `/brightness` | `{"brightness": N}`               | Set brightness (1-100) |
