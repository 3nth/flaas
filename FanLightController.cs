using LibreHardwareMonitor.Hardware;

namespace flaas;

public class FanLightController
{
    private readonly ISensor _sensor;
    private float _brightness = 75;
    private bool _isOn;

    public FanLightController(ISensor sensor)
    {
        _sensor = sensor;
        _ = Get();
    }

    public void On()
    {
        _isOn = true;
        SetBrightness(_brightness);

    }

    public void Off()
    {
        _isOn = false;
        _sensor.Control.SetSoftware(0);

    }

    public State Get()
    {
        _sensor.Hardware.Update();
        _isOn = _sensor.Value > 0;
        if(_isOn)
            _brightness = (int)(_sensor.Value ?? 0);
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
        _brightness = level;
        if(_isOn)
            _sensor.Control.SetSoftware(_brightness);
    }

    public static FanLightController CreateFanLightController(string name)
    {
        Computer computer = new Computer
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

        IEnumerable<ISensor> EnumerateSensors(IEnumerable<IHardware> hardware)
        {
            foreach (var hw in hardware)
            {
                foreach (var s in hw.Sensors)
                    yield return s;
                foreach (var s in EnumerateSensors(hw.SubHardware))
                    yield return s;
            }
        }

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
