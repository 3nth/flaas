using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace flaas;

public class FanLightController
{
    private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");

    private readonly ISensor _sensor;
    private readonly float _hardwareMin;
    private readonly float _hardwareMax;
    private readonly ILogger<FanLightController> _logger;
    private readonly Lock _stateLock = new();
    private float _brightness = 75;
    private bool _isOn;

    public event Action<State>? StateChanged;

    public FanLightController(ISensor sensor, ILogger<FanLightController> logger, float hardwareMin = 1, float hardwareMax = 100)
    {
        _sensor = sensor;
        _logger = logger;
        _hardwareMin = hardwareMin;
        _hardwareMax = hardwareMax;
        LoadState();
        if (_isOn)
            On();
    }

    public void On()
    {
        lock (_stateLock)
        {
            _isOn = true;
            SetBrightnessInternal(_brightness);
            SaveState();
        }
    }

    public void Off()
    {
        lock (_stateLock)
        {
            _isOn = false;
            _sensor.Control.SetSoftware(0);
            SaveState();
        }
    }

    public State Get()
    {
        lock (_stateLock)
            return new State(_isOn, _brightness);
    }

    public void Set(State state)
    {
        lock (_stateLock)
        {
            _brightness = state.Brightness;
            _isOn = state.IsOn;

            if (_isOn)
                SetBrightnessInternal(_brightness);
            else
                _sensor.Control.SetSoftware(0);

            SaveState();
        }
    }

    public void SetBrightness(float level)
    {
        lock (_stateLock)
        {
            SetBrightnessInternal(level);
            SaveState();
        }
    }

    private void SetBrightnessInternal(float level)
    {
        _brightness = Math.Clamp(level, 1, 100);
        if (_isOn)
        {
            var hardware = _hardwareMin + (_brightness / 100f) * (_hardwareMax - _hardwareMin);
            _sensor.Control.SetSoftware(hardware);
        }
    }

    private void SaveState()
    {
        var state = new State(_isOn, _brightness);
        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(StatePath, json);
        StateChanged?.Invoke(state);
    }

    private void LoadState()
    {
        if (!File.Exists(StatePath))
            return;
        try
        {
            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<State>(json);
            if (state is null) return;
            _isOn = state.IsOn;
            _brightness = state.Brightness;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to load state from {Path}, using defaults", StatePath); }
    }

    public static List<string> ListControlSensors()
    {
        var computer = CreateComputer();
        return EnumerateSensors(computer.Hardware)
            .Where(s => s.SensorType == SensorType.Control)
            .Select(s => s.Name)
            .ToList();
    }

    public static FanLightController CreateFanLightController(string? name, ILogger<FanLightController> logger, float hardwareMin = 1, float hardwareMax = 100)
    {
        var computer = CreateComputer();

        var allSensors = EnumerateSensors(computer.Hardware)
            .Where(s => s.SensorType == SensorType.Control)
            .ToList();

        var sensor = allSensors.SingleOrDefault(s => s.Name == name);

        if (sensor is null)
        {
            var available = allSensors.Count > 0
                ? string.Join(", ", allSensors.Select(s => $"\"{s.Name}\""))
                : "(none found)";
            throw new InvalidOperationException(
                $"Sensor \"{name}\" not found. Available control sensors: {available}");
        }

        return new FanLightController(sensor, logger, hardwareMin, hardwareMax);
    }

    private static Computer CreateComputer()
    {
        var computer = new Computer
        {
            IsCpuEnabled = false,
            IsGpuEnabled = false,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = false,
            IsStorageEnabled = false
        };

        computer.Open();
        computer.Accept(new UpdateVisitor());
        return computer;
    }

    private static IEnumerable<ISensor> EnumerateSensors(IEnumerable<IHardware> hardware)
    {
        foreach (var hw in hardware)
        {
            foreach (var s in hw.Sensors)
                yield return s;
            foreach (var s in EnumerateSensors(hw.SubHardware))
                yield return s;
        }
    }
}

public record State(bool IsOn, float Brightness);

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
