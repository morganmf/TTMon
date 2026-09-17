namespace TTMon;

// Serce aplikacji - bez glownego okna (ApplicationContext zamiast Form).
// Trzyma NotifyIcon, timer odswiezania i wszystkie trzy zrodla danych.
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly SensorReader _sensorReader;
    private readonly WanMonitor _wanMonitor;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;

    private SensorSnapshot _lastSnapshot = new();
    private int? _lastWanLatencyMs;
    private readonly HistoryBuffer _history = new();

    private readonly ToolStripMenuItem _infoMenuItem;
    private readonly ToolStripMenuItem _sensorsMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        Localization.CurrentLanguage = _settings.Language;

        _sensorReader = new SensorReader(_settings);
        _wanMonitor = new WanMonitor();

        // Samo-naprawa: jesli autostart jest wlaczony, odswiezamy zadanie na
        // BIEZACA sciezke .exe przy kazdym starcie. Bez tego przeniesienie
        // programu (nowy build, przeinstalowanie w innym miejscu) zostawia
        // zadanie wskazujace na stary, martwy plik - z pozoru wlaczone
        // (IsEnabled() widzi tylko czy zadanie ISTNIEJE, nie czy sciezka jest
        // aktualna), ale realnie nic nie uruchamia przy logowaniu.
        if (AutostartManager.IsEnabled())
            AutostartManager.SetEnabled(true);

        var splashImage = BrandingImage.Load();
        if (_settings.ShowSplash && splashImage != null)
        {
            var splash = new SplashForm(splashImage);
            splash.Show(); // niemodalny - reszta inicjalizacji (tray, timer) leci od razu obok
        }

        _infoMenuItem = new ToolStripMenuItem(Localization.T("info"), null, OnInfoClicked);
        _sensorsMenuItem = new ToolStripMenuItem(Localization.T("sensors"), null, OnSensorsClicked);
        _settingsMenuItem = new ToolStripMenuItem(Localization.T("settings"), null, OnSettingsClicked);
        _exitMenuItem = new ToolStripMenuItem(Localization.T("exit"), null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_infoMenuItem);
        menu.Items.Add(_sensorsMenuItem);
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
            Icon = TrayIconRenderer.Render("...", Color.Gray, _settings.IconSize, _settings.TrayFont, _settings.IconOutline),
            Text = AppInfo.AppName,
        };
        // Sam klik lewym otwiera wykresy - bez opoznienia na podwojny klik,
        // bo dwuklik juz nie robi nic osobnego (wczesniej otwieral ustawienia,
        // usuniete na zyczenie - ustawienia sa tylko w menu prawego klikniecia).
        _trayIcon.MouseClick += OnTrayIconMouseClick;

        _timer = new System.Windows.Forms.Timer { Interval = _settings.RefreshIntervalMs };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        // Pierwsze odswiezenie od razu, bez czekania na pierwszy tick timera
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _lastSnapshot = _sensorReader.Read();

        if (_settings.ShowWan)
            _lastWanLatencyMs = await _wanMonitor.MeasureLatencyMsAsync(_settings.WanPingHost);

        var motherboardSensors = _sensorReader.GetMotherboardSensors();
        // Niektore plyty maja WIECEJ NIZ JEDEN sensor o tej samej nazwie (np.
        // "Chipset" jako napiecie ORAZ jako osobna temperatura) - sama nazwa
        // nie jest unikalnym kluczem. GroupBy+First zamiast ToDictionary,
        // ktore rzucaloby wyjatek na pierwszym duplikacie (co sie realnie
        // zdarzylo - "Chipset" 0.3V i "Chipset" 40C na tej samej plycie).
        var motherboardDict = motherboardSensors
            .Where(s => s.Value.HasValue)
            .GroupBy(s => $"{s.Name} ({s.Unit})")
            .ToDictionary(g => g.Key, g => g.First().Value!.Value);

        _history.Add(new HistorySample(DateTime.UtcNow, _lastSnapshot.CpuTempC, _lastSnapshot.GpuTempC, _lastSnapshot.VrmTempC, _lastWanLatencyMs, motherboardDict));

        if (_settings.EnableLogging)
            ReadingsLogger.Log(DateTime.Now, _lastSnapshot.CpuTempC, _lastSnapshot.GpuTempC, _lastSnapshot.VrmTempC, _lastWanLatencyMs);

        UpdateTrayIcon();
    }

    private void UpdateTrayIcon()
    {
        // Priorytet wyswietlania w ikonie: CPU > GPU > VRM > WAN (tylko jedna
        // wartosc miesci sie sensownie w malej ikonie) - reszta trafia do
        // tooltipa, a wszystkie razem do okienka podgladu (klik lewym).
        string iconText;
        Color iconColor;

        if (_settings.ShowCpu && _lastSnapshot.CpuTempC is float cpu)
        {
            // MathF.Round, nie (int)cpu - rzutowanie na int UCINA czesc
            // ulamkowa (44.6 -> 44) zamiast zaokraglac (44.6 -> 45), co dawalo
            // niespojnosc z oknem podgladu (DetailsForm zaokragla przez "0").
            iconText = $"{MathF.Round(cpu)}";
            var t = GradientPalette.Normalize(cpu, _settings.TempGradientMinC, _settings.TempGradientMaxC);
            iconColor = GradientPalette.Sample(t);
        }
        else if (_settings.ShowGpu && _lastSnapshot.GpuTempC is float gpu)
        {
            iconText = $"{MathF.Round(gpu)}";
            var t = GradientPalette.Normalize(gpu, _settings.TempGradientMinC, _settings.TempGradientMaxC);
            iconColor = GradientPalette.Sample(t);
        }
        else if (_settings.ShowVrm && _lastSnapshot.VrmTempC is float vrm)
        {
            iconText = $"{MathF.Round(vrm)}";
            var t = GradientPalette.Normalize(vrm, _settings.TempGradientMinC, _settings.TempGradientMaxC);
            iconColor = GradientPalette.Sample(t);
        }
        else if (_settings.ShowWan)
        {
            iconText = _lastWanLatencyMs?.ToString() ?? "--";
            var t = GradientPalette.Normalize(
                _lastWanLatencyMs ?? _settings.WanGradientMinMs,
                _settings.WanGradientMinMs, _settings.WanGradientMaxMs);
            iconColor = GradientPalette.Sample(t);
        }
        else
        {
            iconText = "-";
            iconColor = Color.Gray;
        }

        var oldIcon = _trayIcon.Icon;
        _trayIcon.Icon = TrayIconRenderer.Render(iconText, iconColor, _settings.IconSize, _settings.TrayFont, _settings.IconOutline);
        oldIcon?.Dispose();

        _trayIcon.Text = BuildTooltip();
    }

    private string BuildTooltip()
    {
        var parts = new List<string>();
        if (_settings.ShowCpu)
            parts.Add($"CPU: {(_lastSnapshot.CpuTempC is float c ? $"{c:0}\u00b0C" : "n/a")}");
        if (_settings.ShowGpu)
            parts.Add($"GPU: {(_lastSnapshot.GpuTempC is float g ? $"{g:0}\u00b0C" : "n/a")}");
        if (_settings.ShowVrm)
            parts.Add($"VRM: {(_lastSnapshot.VrmTempC is float v ? $"{v:0}\u00b0C" : "n/a")}");
        if (_settings.ShowWan)
            parts.Add($"WAN: {(_lastWanLatencyMs is int ms ? $"{ms} ms" : Localization.T("wan_offline"))}");

        // NotifyIcon.Text ma limit 63 znakow w WinForms
        var text = string.Join("  |  ", parts);
        return text.Length > 63 ? text[..63] : text;
    }

    private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ShowDetailsForm();
    }

    private void ShowDetailsForm()
    {
        using var form = new DetailsForm(() => _lastSnapshot, () => _lastWanLatencyMs, () => _history.Snapshot(), _settings);
        form.ShowDialog();
    }

    private void OnInfoClicked(object? sender, EventArgs e)
    {
        var (cpuName, gpuName) = _sensorReader.GetHardwareNames();
        var ramGb = SystemInfo.GetTotalRamGb();
        using var form = new InfoForm(cpuName, gpuName, ramGb, _settings);
        form.ShowDialog();
    }

    private void OnSensorsClicked(object? sender, EventArgs e)
    {
        using var form = new SensorsForm(() => _sensorReader.GetMotherboardSensors(), () => _history.Snapshot(), _settings);
        form.ShowDialog();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            Localization.CurrentLanguage = _settings.Language;
            _timer.Interval = _settings.RefreshIntervalMs;
            _infoMenuItem.Text = Localization.T("info");
            _sensorsMenuItem.Text = Localization.T("sensors");
            _settingsMenuItem.Text = Localization.T("settings");
            _exitMenuItem.Text = Localization.T("exit");
            _ = RefreshAsync(); // od razu odswiez ikone - nowy rozmiar/gradient/kontur
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _timer.Stop();
        _sensorReader.Dispose();
        Application.Exit();
    }
}
