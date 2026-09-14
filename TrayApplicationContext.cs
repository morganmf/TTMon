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

    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _infoMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    // Windows ZAWSZE wysyla pojedyncze klikniecie (Click) jako pierwsza polowe
    // podwojnego kliknieca - bez tego opoznienia klikniecie x2 otwieraloby i
    // wykresy (od pojedynczego) i ustawienia (od podwojnego) za kazdym razem.
    private readonly System.Windows.Forms.Timer _singleClickTimer = new() { Interval = SystemInformation.DoubleClickTime };

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        Localization.CurrentLanguage = _settings.Language;

        _sensorReader = new SensorReader(_settings);
        _wanMonitor = new WanMonitor();

        var splashImage = BrandingImage.Load();
        if (_settings.ShowSplash && splashImage != null)
        {
            var splash = new SplashForm(splashImage);
            splash.Show(); // niemodalny - reszta inicjalizacji (tray, timer) leci od razu obok
        }

        _settingsMenuItem = new ToolStripMenuItem(Localization.T("settings"), null, OnSettingsClicked);
        _infoMenuItem = new ToolStripMenuItem(Localization.T("info"), null, OnInfoClicked);
        _exitMenuItem = new ToolStripMenuItem(Localization.T("exit"), null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_infoMenuItem);
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
            Icon = TrayIconRenderer.Render("...", Color.Gray, _settings.IconSize, _settings.TrayFont),
            Text = AppInfo.AppName,
        };
        _trayIcon.MouseClick += OnTrayIconMouseClick;
        _trayIcon.MouseDoubleClick += OnTrayIconMouseDoubleClick;
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            ShowDetailsForm();
        };

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

        _history.Add(new HistorySample(DateTime.UtcNow, _lastSnapshot.CpuTempC, _lastSnapshot.GpuTempC, _lastSnapshot.VrmTempC, _lastWanLatencyMs));

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
        _trayIcon.Icon = TrayIconRenderer.Render(iconText, iconColor, _settings.IconSize, _settings.TrayFont);
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
        // Start/restart opoznionego pojedynczego kliknieca - jesli w tym czasie
        // przyjdzie MouseDoubleClick, ten timer zostanie tam zatrzymany i
        // wykresy sie nie otworza.
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void OnTrayIconMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _singleClickTimer.Stop(); // anuluj oczekujace pojedyncze klikniecie
        OnSettingsClicked(sender, e);
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
        var motherboardSensors = _sensorReader.GetMotherboardSensors();
        using var form = new InfoForm(cpuName, gpuName, ramGb, motherboardSensors);
        form.ShowDialog();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            Localization.CurrentLanguage = _settings.Language;
            _timer.Interval = _settings.RefreshIntervalMs;
            _settingsMenuItem.Text = Localization.T("settings");
            _infoMenuItem.Text = Localization.T("info");
            _exitMenuItem.Text = Localization.T("exit");
            _ = RefreshAsync(); // od razu odswiez ikone - nowy rozmiar/gradient
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _timer.Stop();
        _singleClickTimer.Stop();
        _sensorReader.Dispose();
        Application.Exit();
    }
}
