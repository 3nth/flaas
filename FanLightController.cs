using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace flaas;

public class FanLightController
{
    private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");

    private readonly ISensor _sensor;
    private float _brightness = 75;
    private bool _isOn;

    public FanLightController(ISensor sensor)
    {
        _sensor = sensor;
        LoadState();
        if (_isOn)
            On();
    }

    public void On()
    {
        _isOn = true;
        SetBrightness(_brightness);
        SaveState();
    }

    public void Off()
    {
        _isOn = false;
        _sensor.Control.SetSoftware(0);
        SaveState();
    }

    public State Get()
    {
        _sensor.Hardware.Update();
        var hwValue = (int)(_sensor.Value ?? 0);

        // Detect external changes, but ignore hardware flicker (±1)
        if (!_isOn && hwValue > 1)
            _isOn = true;
        else if (_isOn && hwValue == 0)
            _isOn = false;

        return new State(_isOn, _brightness);
    }

    public void Set(State state)
    {
        _brightness = state.Brightness;
        _isOn = state.IsOn;

        if(_isOn)
            On();
        else
            Off();
    }

    public void SetBrightness(float level)
    {
        _brightness = Math.Clamp(level, 1, 100);
        if(_isOn)
            _sensor.Control.SetSoftware(_brightness);
        SaveState();
    }

    private void SaveState()
    {
        var json = JsonSerializer.Serialize(new State(_isOn, _brightness));
        File.WriteAllText(StatePath, json);
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
        catch (Exception) { /* corrupt state file, use defaults */ }
    }

    public static List<string> ListControlSensors()
    {
        var computer = CreateComputer();
        return EnumerateSensors(computer.Hardware)
            .Where(s => s.SensorType == SensorType.Control)
            .Select(s => s.Name)
            .ToList();
    }

    public static FanLightController CreateFanLightController(string name)
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

        return new FanLightController(sensor);
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
