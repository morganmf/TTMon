namespace TTMon;

public static class Localization
{
    private static readonly Dictionary<string, (string Pl, string En)> Strings = new()
    {
        ["settings"]        = ("Ustawienia...", "Settings..."),
        ["info"]            = ("Info", "Info"),
        ["exit"]            = ("Zakończ", "Exit"),

        ["settings_title"]  = ("Ustawienia TTMon", "TTMon Settings"),
        ["section_general"] = ("Ogólne", "General"),
        ["section_sensors"] = ("Czujniki", "Sensors"),
        ["section_gradient_temp"] = ("Gradient temperatury (CPU/GPU)", "Temperature gradient (CPU/GPU)"),
        ["section_gradient_wan"]  = ("Gradient opóźnienia WAN", "WAN latency gradient"),

        ["language"]  = ("Język", "Language"),
        ["autostart"]     = ("Uruchamiaj z Windows", "Start with Windows"),
        ["autostart_failed"] = ("Nie udało się zmienić ustawienia autostartu (sprawdź uprawnienia).", "Failed to change autostart setting (check permissions)."),
        ["show_splash"]   = ("Pokazuj ekran powitalny", "Show splash screen"),
        ["enable_logging"] = ("Zapisuj odczyty do pliku .csv", "Log readings to .csv file"),
        ["details_always_on_top"] = ("Okienko z wykresami zawsze na wierzchu", "Charts window always on top"),
        ["icon_size"]        = ("Wielkość tekstu w ikonie", "Icon text size"),
        ["icon_size_small"]  = ("Mały", "Small"),
        ["icon_size_medium"] = ("Średni", "Medium"),
        ["icon_size_large"]  = ("Duży", "Large"),

        ["show_cpu"] = ("Pokazuj temperaturę CPU", "Show CPU temperature"),
        ["show_gpu"] = ("Pokazuj temperaturę GPU", "Show GPU temperature"),
        ["show_wan"] = ("Pokazuj opóźnienie WAN", "Show WAN latency"),
        ["show_vrm"] = ("Pokazuj temperaturę VRM MOS", "Show VRM MOS temperature"),
        ["show_cpu_fan"] = ("Pokazuj obroty wentylatora CPU", "Show CPU fan speed"),

        ["tray_font"]             = ("Czcionka ikony", "Icon font"),
        ["tray_font_segoe"]       = ("Segoe UI (domyślna)", "Segoe UI (default)"),
        ["tray_font_bahnschrift"] = ("Bahnschrift", "Bahnschrift"),
        ["tray_font_dosis"]       = ("Dosis", "Dosis"),
        ["tray_font_jetbrains"]   = ("JetBrains Mono", "JetBrains Mono"),
        ["tray_font_firacode_mono"]   = ("Fira Code Mono", "Fira Code Mono"),
        ["tray_font_envycoder_mono"]  = ("Envy Code R Mono", "Envy Code R Mono"),
        ["tray_font_terminess_mono"]  = ("Terminess Mono", "Terminess Mono"),
        ["tray_font_meslo"]           = ("MesloLGL Mono", "MesloLGL Mono"),
        ["tray_font_hurmit_mono"]     = ("Hurmit Mono", "Hurmit Mono"),

        ["cpu_vendor"]       = ("Producent CPU", "CPU vendor"),
        ["cpu_vendor_auto"]  = ("Automatycznie", "Automatic"),
        ["cpu_vendor_intel"] = ("Intel", "Intel"),
        ["cpu_vendor_amd"]   = ("AMD", "AMD"),

        ["gpu_vendor"]        = ("Producent GPU", "GPU vendor"),
        ["gpu_vendor_auto"]   = ("Automatycznie", "Automatic"),
        ["gpu_vendor_intel"]  = ("Intel", "Intel"),
        ["gpu_vendor_amd"]    = ("AMD (Radeon)", "AMD (Radeon)"),
        ["gpu_vendor_nvidia"] = ("NVIDIA", "NVIDIA"),

        ["gradient_temp_min"] = ("Minimum (°C)", "Minimum (°C)"),
        ["gradient_temp_max"] = ("Maksimum (°C)", "Maximum (°C)"),
        ["gradient_wan_min"]  = ("Minimum (ms)", "Minimum (ms)"),
        ["gradient_wan_max"]  = ("Maksimum (ms)", "Maximum (ms)"),

        ["close"]  = ("Zamknij", "Close"),

        ["wan_offline"] = ("brak", "n/a"),

        ["info_title"]      = ("O programie", "About"),
        ["info_author"]     = ("Autor", "Author"),
        ["info_build_date"] = ("Data kompilacji", "Build date"),
        ["info_cpu"]        = ("CPU", "CPU"),
        ["info_gpu"]        = ("GPU", "GPU"),
        ["info_ram"]        = ("RAM", "RAM"),
        ["info_unknown"]    = ("nie wykryto", "not detected"),

        ["sensors"]              = ("Sensory", "Sensors"),
        ["sensors_title"]        = ("Czujniki płyty głównej", "Motherboard sensors"),
        ["sensors_col_name"]     = ("Nazwa", "Name"),
        ["sensors_col_value"]    = ("Wartość", "Value"),
        ["sensors_col_min"]      = ("Min", "Min"),
        ["sensors_col_max"]      = ("Max", "Max"),
        ["sensors_col_avg"]      = ("Śr.", "Avg"),
        ["sensors_hide_inactive"] = ("Ukryj nieaktywne (wartość 0)", "Hide inactive (value 0)"),
        ["sensors_select_hint"]  = ("Wybierz czujnik z listy", "Select a sensor from the list"),

        ["save"]         = ("Zapisz", "Save"),
        ["dark_mode"]    = ("Tryb ciemny", "Dark mode"),
        ["icon_outline"] = ("Kontur wokół cyfr w ikonie", "Icon digit outline"),
        ["icon_background_plate"] = ("Jasne tło pod cyframi w ikonie", "Light background behind icon digits"),
        ["icon_color_mode"]        = ("Styl kolorowania ikony", "Icon coloring style"),
        ["icon_color_mode_text"]   = ("Kolorowy tekst (domyślnie)", "Colored text (default)"),
        ["icon_color_mode_bg"]     = ("Kolorowe tło, auto-kontrast tekstu", "Colored background, auto-contrast text"),

        ["details_title"] = ("TTMon - podgląd", "TTMon - overview"),
    };

    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.PL;

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var pair)) return key;
        return CurrentLanguage == AppLanguage.PL ? pair.Pl : pair.En;
    }
}
