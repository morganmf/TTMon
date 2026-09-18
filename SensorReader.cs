using LibreHardwareMonitor.Hardware;

namespace TTMon;

public sealed class SensorSnapshot
{
    public float? CpuTempC { get; init; }
    public float? GpuTempC { get; init; }
    public float? VrmTempC { get; init; }
    public float? CpuFanRpm { get; init; }
}

// Cienka warstwa nad LibreHardwareMonitorLib. Otwiera dostep do sprzetu raz,
// przy kazdym odczycie tylko aktualizuje wartosci (Open() jest kosztowne,
// Update() jest tanie - stad rozdzielenie).
public sealed class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private readonly AppSettings _settings;

    public SensorReader(AppSettings settings)
    {
        _settings = settings;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true, // na probe - wszystkie sensory pod SuperIO, patrz GetMotherboardSensors()
            IsMemoryEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
        };
        _computer.Open();
    }

    // Wszystkie sensory zglaszane przez plyte glowna (SuperIO - temperatury,
    // obroty wentylatorow, napiecia) - na probe do okna Info, zeby zobaczyc co
    // faktycznie zglasza dany model plyty przed zdecydowaniem co pokazywac na stale.
    public List<(string Name, float? Value, string Unit)> GetMotherboardSensors()
    {
        var result = new List<(string Name, float? Value, string Unit)>();

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Motherboard) continue;

            hardware.Update();
            CollectSensors(hardware, result);

            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
                CollectSensors(sub, result);
            }
        }

        return result;
    }

    private static void CollectSensors(IHardware hardware, List<(string Name, float? Value, string Unit)> result)
    {
        foreach (var sensor in hardware.Sensors)
        {
            var unit = sensor.SensorType switch
            {
                SensorType.Temperature => "\u00b0C",
                SensorType.Fan => "RPM",
                SensorType.Voltage => "V",
                SensorType.Control => "%",
                SensorType.Load => "%",
                _ => "",
            };
            result.Add((sensor.Name, sensor.Value, unit));
        }
    }

    // Nazwy wykrytego sprzetu (np. "AMD Ryzen 7 7800X3D", "AMD Radeon RX 7800 XT") -
    // dostepne od razu po Open(), nie wymagaja Update()/sensorow (do okna Info)
    public (string? CpuName, string? GpuName) GetHardwareNames()
    {
        string? cpuName = null;
        string? gpuName = null;
        var cpuPreference = _settings.PreferredCpuVendor;
        var gpuPreference = _settings.PreferredGpuVendor;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType == HardwareType.Cpu && IsMatchingCpuName(hardware.Name, cpuPreference))
                cpuName ??= hardware.Name;
            else if (IsMatchingGpuType(hardware.HardwareType, gpuPreference))
                gpuName ??= hardware.Name;
        }

        return (cpuName, gpuName);
    }

    public SensorSnapshot Read()
    {
        float? cpuTemp = null;
        float? gpuTemp = null;
        float? vrmTemp = null;
        float? cpuFanRpm = null;

        // Czytane przy kazdym odswiezeniu, zeby zmiana w ustawieniach dzialala
        // od razu, bez restartu SensorReadera.
        var cpuPreference = _settings.PreferredCpuVendor;
        var gpuPreference = _settings.PreferredGpuVendor;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            bool isMatchingCpu = hardware.HardwareType == HardwareType.Cpu
                                 && IsMatchingCpuName(hardware.Name, cpuPreference);
            bool isMatchingGpu = IsMatchingGpuType(hardware.HardwareType, gpuPreference);
            bool isMotherboard = hardware.HardwareType == HardwareType.Motherboard;

            if (isMatchingCpu)
            {
                cpuTemp = FindPackageTemperature(hardware) ?? cpuTemp;
            }
            else if (isMatchingGpu)
            {
                // Przy Auto i kilku kartach w systemie bierzemy pierwsza znaleziona,
                // ktora faktycznie zwraca temperature - kolejnosc enumeracji przez
                // LibreHardwareMonitorLib nie jest gwarantowana, ale w praktyce
                // stabilna dla danego systemu miedzy uruchomieniami.
                gpuTemp ??= FindPackageTemperature(hardware);
            }
            else if (isMotherboard)
            {
                vrmTemp ??= FindNamedTemperature(hardware, "VRM MOS");
                // "CPU Fan" wystepuje na wielu plytach DWA razy - jako Control
                // (procent PWM) i jako Fan (RPM). Bierzemy konkretnie RPM -
                // bardziej czytelne/uzyteczne jako "predkosc wentylatora" niz
                // surowy procent sterowania.
                cpuFanRpm ??= FindNamedFanRpm(hardware, "CPU Fan");
            }

            // Niektore plytki/GPU zglaszaja dodatkowe czujniki jako "SubHardware"
            // (na plycie glownej to wlasnie SuperIO, gdzie zwykle siedzi VRM MOS)
            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
                if (isMatchingCpu && cpuTemp == null)
                    cpuTemp = FindPackageTemperature(sub);
                else if (isMatchingGpu && gpuTemp == null)
                    gpuTemp = FindPackageTemperature(sub);
                else if (isMotherboard)
                {
                    if (vrmTemp == null) vrmTemp = FindNamedTemperature(sub, "VRM MOS");
                    if (cpuFanRpm == null) cpuFanRpm = FindNamedFanRpm(sub, "CPU Fan");
                }
            }
        }

        return new SensorSnapshot { CpuTempC = cpuTemp, GpuTempC = gpuTemp, VrmTempC = vrmTemp, CpuFanRpm = cpuFanRpm };
    }

    private static bool IsMatchingCpuName(string? hardwareName, CpuVendorPreference preference)
    {
        if (preference == CpuVendorPreference.Auto) return true;
        var name = hardwareName ?? string.Empty;
        return preference switch
        {
            CpuVendorPreference.Intel => name.Contains("Intel", StringComparison.OrdinalIgnoreCase),
            CpuVendorPreference.Amd => name.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                     || name.Contains("Ryzen", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private static bool IsMatchingGpuType(HardwareType type, GpuVendorPreference preference)
    {
        bool isAnyGpu = type == HardwareType.GpuAmd
                      || type == HardwareType.GpuNvidia
                      || type == HardwareType.GpuIntel;
        if (!isAnyGpu) return false;

        return preference switch
        {
            GpuVendorPreference.Amd => type == HardwareType.GpuAmd,
            GpuVendorPreference.Intel => type == HardwareType.GpuIntel,
            GpuVendorPreference.Nvidia => type == HardwareType.GpuNvidia,
            _ => true, // Auto - dowolna wykryta karta
        };
    }

    // Szuka konkretnego, nazwanego czujnika temperatury (np. "VRM MOS") -
    // dokladne dopasowanie nazwy, w odroznieniu od FindPackageTemperature
    // ktore probuje zgadnac "glowny" czujnik CPU/GPU po wzorcach nazw.
    private static float? FindNamedTemperature(IHardware hardware, string sensorName)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature &&
                sensor.Name.Equals(sensorName, StringComparison.OrdinalIgnoreCase))
                return sensor.Value;
        }
        return null;
    }

    // Jak FindNamedTemperature, tylko dla czujnikow typu Fan (RPM).
    private static float? FindNamedFanRpm(IHardware hardware, string sensorName)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Fan &&
                sensor.Name.Equals(sensorName, StringComparison.OrdinalIgnoreCase))
                return sensor.Value;
        }
        return null;
    }

    private static float? FindPackageTemperature(IHardware hardware)
    {
        // Preferuj czujnik "Package"/"Core (Tctl/Tdie)" (Ryzen/Intel) albo "GPU Core"
        // (Radeon/Arc/GeForce), w razie braku - pierwszy dostepny czujnik Temperature.
        ISensor? best = null;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature) continue;

            if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
             || sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
             || sensor.Name.Contains("Core (Tctl", StringComparison.OrdinalIgnoreCase)
             || sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))
            {
                return sensor.Value;
            }

            best ??= sensor;
        }
        return best?.Value;
    }

    public void Dispose() => _computer.Close();
}
