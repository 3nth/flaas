using LibreHardwareMonitor.Hardware;

namespace flaas;

public class FanLightController
{
    private readonly ISensor? _sensor;
    private int _brightness = 75;

    public FanLightController(ISensor? sensor)
    {
        _sensor = sensor;

    }

    public void On()
    {
        _sensor?.Control.SetSoftware(_brightness);

    }

    public void Off()
    {
        _sensor?.Control.SetSoftware(0);

    }

    public Level Get()
    {
        _sensor?.Hardware.Update();
        return new Level((int)(_sensor?.Value ?? 0));
    }

    public void Set(int level)
    {
        _brightness = level;
        _sensor?.Control.SetSoftware(level);
    }

    public static FanLightController CreateFanLightController(string name)
    {
        Computer computer = new Computer
        {
            IsCpuEnabled = false,
            IsGpuEnabled = false,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = true,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsStorageEnabled = false
        };

        computer.Open();
        computer.Accept(new UpdateVisitor());

        var sensor = computer.Hardware.SelectMany(h => h.SubHardware).SelectMany(sh => sh.Sensors)
            .SingleOrDefault(s => s.SensorType == SensorType.Control && s.Name == name);

        return new FanLightController(sensor);

    }
}

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
