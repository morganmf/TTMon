using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTMon;

public enum AppLanguage { PL, EN }

// LibreHardwareMonitorLib nie rozroznia CPU po producencie w HardwareType (jest
// tylko jeden typ "Cpu") - dopasowanie po nazwie sprzetu (Intel/AMD w nazwie).
// W praktyce jeden komputer ma jeden fizyczny CPU, wiec "Auto" wystarcza -
// ten wybor sluzy glownie gdy Auto z jakiegos powodu trafi zly odczyt.
public enum CpuVendorPreference { Auto, Intel, Amd }

// Do wyboru gdy w systemie jest wiecej niz jedna karta graficzna (np. iGPU +
// dedykowana karta) - Auto bierze pierwsza znaleziona, ktora zwraca temperature.
public enum GpuVendorPreference { Auto, Intel, Amd, Nvidia }

// Realnego rozmiaru ikony w trayu nie da sie zmienic (patrz TrayIconRenderer),
// wiec to reguluje jak duzo miejsca w obrebie tego stalego kwadratu zajmuje
// tekst - z automatycznym dopasowaniem, zeby nigdy sie nie ucinal.
public enum IconSizeLevel { Small, Medium, Large }

// Bahnschrift jest wbudowany w Windows 10/11 (dziala od razu). Dosis i
// JetBrainsMono NIE sa czcionkami systemowymi - wymagaja plikow w Resources/
// dostarczonych przez usera i osadzonych w binarce (patrz EmbeddedFontLoader.cs);
// jesli pliku nie ma, wybor cicho spada z powrotem na Segoe UI.
public enum TrayFontChoice { SegoeUI, Bahnschrift, Dosis, JetBrainsMono }

public class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.PL;

    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowWan { get; set; } = true;
    public bool ShowVrm { get; set; } = true;

    public CpuVendorPreference PreferredCpuVendor { get; set; } = CpuVendorPreference.Auto;
    public GpuVendorPreference PreferredGpuVendor { get; set; } = GpuVendorPreference.Auto;
    public IconSizeLevel IconSize { get; set; } = IconSizeLevel.Medium;
    public TrayFontChoice TrayFont { get; set; } = TrayFontChoice.Bahnschrift;

    public bool ShowSplash { get; set; } = true;
    public bool EnableLogging { get; set; } = false;
    public bool DetailsAlwaysOnTop { get; set; } = false;
    public int? DetailsWindowX { get; set; }
    public int? DetailsWindowY { get; set; }
    public bool IconOutline { get; set; } = true;
    public bool DarkMode { get; set; } = false;

    // Adres pingowany do pomiaru opoznienia WAN (musi odpowiadac na ICMP)
    public string WanPingHost { get; set; } = "1.1.1.1";

    // Zakres gradientu temperatury (stopnie C), wspolny dla CPU i GPU -
    // ponizej Min = najzimniejszy kolor gradientu, powyzej Max = najgoretszy
    public float TempGradientMinC { get; set; } = 30f;
    public float TempGradientMaxC { get; set; } = 90f;

    // Zakres gradientu opoznienia WAN (milisekundy)
    public int WanGradientMinMs { get; set; } = 0;
    public int WanGradientMaxMs { get; set; } = 200;

    public int RefreshIntervalMs { get; set; } = 2000;

    [JsonIgnore]
    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TTMon", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Uszkodzony/niekompatybilny plik ustawien - wracamy do domyslnych
        }
        return new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
