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

    // KAZDE z tych okien moze byc otwarte tylko RAZ naraz - kolejne klikniecie
    // aktywuje juz otwarte zamiast tworzyc nowe. Bez tego (poprzedni blad):
    // ShowDialog() blokuje watek UI WLASNA, zagniezdzona petla komunikatow,
    // ale ta petla DALEJ obsluguje klikniecia w ikone trayu - kazde kolejne
    // klikniecie w trakcie otwierania otwieralo NASTEPNE okno wewnatrz
    // poprzedniego, w nieskonczonosc. Rozwiazanie: niemodalne Show() (nie
    // ShowDialog()) + trzymanie jednej referencji per typ okna.
    private DetailsForm? _detailsForm;
    private InfoForm? _infoForm;
    private SensorsForm? _sensorsForm;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        Localization.CurrentLanguage = _settings.Language;

        _sensorReader = new SensorReader(_settings);
        _wanMonitor = new WanMonitor();

        // Samo-naprawa: jesli autostart jest wlaczony, odswiezamy zadanie na
        // BIEZACA sciezke .exe przy kazdym starcie. Bez tego przeniesienie
        // programu (nowy build, przeinstalowanie w innym miejscu) zostawia
        // zadanie wskazujace na stary, martwy plik.
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
            Icon = TrayIconRenderer.Render("...", Color.Gray, _settings.IconSize, _settings.TrayFont, _settings.IconOutline, _settings.IconBackgroundPlate, _settings.IconColorMode),
            Text = AppInfo.AppName,
        };
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
        // ktore rzucaloby wyjatek na pierwszym duplikacie.
        var motherboardDict = motherboardSensors
            .Where(s => s.Value.HasValue)
            .GroupBy(s => $"{s.Name} ({s.Unit})")
            .ToDictionary(g => g.Key, g => g.First().Value!.Value);

        _history.Add(new HistorySample(
            DateTime.UtcNow,
            _lastSnapshot.CpuTempC,
            _lastSnapshot.GpuTempC,
            _lastSnapshot.VrmTempC,
            _lastSnapshot.CpuFanRpm,
            _lastWanLatencyMs,
            motherboardDict));

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
        _trayIcon.Icon = TrayIconRenderer.Render(iconText, iconColor, _settings.IconSize, _settings.TrayFont, _settings.IconOutline, _settings.IconBackgroundPlate, _settings.IconColorMode);
        oldIcon?.Dispose();

        _trayIcon.Text = BuildTooltip();
    }

    private string BuildTooltip()
    {
        // Kolejnosc CELOWA: WAN przed FAN. Gdy trzeba cokolwiek uciac (patrz
        // nizej), leci od konca listy - FAN (najnowszy, najmniej krytyczny)
        // znika pierwszy, WAN zostaje.
        var parts = new List<string>();
        if (_settings.ShowCpu)
            parts.Add($"CPU: {(_lastSnapshot.CpuTempC is float c ? $"{c:0}\u00b0C" : "n/a")}");
        if (_settings.ShowGpu)
            parts.Add($"GPU: {(_lastSnapshot.GpuTempC is float g ? $"{g:0}\u00b0C" : "n/a")}");
        if (_settings.ShowVrm)
            parts.Add($"VRM: {(_lastSnapshot.VrmTempC is float v ? $"{v:0}\u00b0C" : "n/a")}");
        if (_settings.ShowWan)
            parts.Add($"WAN: {(_lastWanLatencyMs is int ms ? $"{ms} ms" : Localization.T("wan_offline"))}");
        if (_settings.ShowCpuFan)
            parts.Add($"FAN: {(_lastSnapshot.CpuFanRpm is float f ? $"{f:0} RPM" : "n/a")}");

        // NotifyIcon.Text ma TWARDY limit 63 znakow w WinForms. Poprzednio
        // ucinalismy string w polowie ("text[..63]") - to zawsze obcinalo
        // WAN, bo byl ostatni na liscie i limit sie wlasnie tam wyrabial po
        // dodaniu CPU FAN. Teraz usuwamy CALE segmenty od konca (nie
        // pojedyncze znaki w srodku slowa), az sie zmiesci - dzieki
        // kolejnosci wyzej, to FAN znika jako pierwszy, nie WAN.
        while (parts.Count > 0 && string.Join("  |  ", parts).Length > 63)
            parts.RemoveAt(parts.Count - 1);

        return string.Join("  |  ", parts);
    }

    private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ShowOrActivate(ref _detailsForm, () =>
        {
            var form = new DetailsForm(() => _lastSnapshot, () => _lastWanLatencyMs, () => _history.Snapshot(), _settings);
            form.FormClosed += (_, _) => _detailsForm = null;
            return form;
        });
    }

    private void OnInfoClicked(object? sender, EventArgs e)
    {
        ShowOrActivate(ref _infoForm, () =>
        {
            var (cpuName, gpuName) = _sensorReader.GetHardwareNames();
            var ramGb = SystemInfo.GetTotalRamGb();
            var form = new InfoForm(cpuName, gpuName, ramGb, _settings);
            form.FormClosed += (_, _) => _infoForm = null;
            return form;
        });
    }

    private void OnSensorsClicked(object? sender, EventArgs e)
    {
        ShowOrActivate(ref _sensorsForm, () =>
        {
            var form = new SensorsForm(() => _sensorReader.GetMotherboardSensors(), () => _history.Snapshot(), _settings);
            form.FormClosed += (_, _) => _sensorsForm = null;
            return form;
        });
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_settings);
        form.FormClosed += (_, _) =>
        {
            if (form.DialogResult == DialogResult.OK)
            {
                Localization.CurrentLanguage = _settings.Language;
                _timer.Interval = _settings.RefreshIntervalMs;
                _infoMenuItem.Text = Localization.T("info");
                _sensorsMenuItem.Text = Localization.T("sensors");
                _settingsMenuItem.Text = Localization.T("settings");
                _exitMenuItem.Text = Localization.T("exit");
                _ = RefreshAsync(); // od razu odswiez ikone - nowy rozmiar/gradient/kontur
            }
            _settingsForm = null;
        };
        _settingsForm = form;
        form.Show();
    }

    // Wspolny wzorzec dla okien bez dodatkowej logiki po zamknieciu (Details/
    // Info/Sensory) - jesli juz otwarte, tylko aktywuje; inaczej tworzy przez
    // podana fabryke (ktora sama podpina czyszczenie referencji po zamknieciu).
    private static void ShowOrActivate<TForm>(ref TForm? field, Func<TForm> factory) where TForm : Form
    {
        if (field != null && !field.IsDisposed)
        {
            field.Activate();
            return;
        }

        field = factory();
        field.Show();
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _timer.Stop();
        _sensorReader.Dispose();
        Application.Exit();
    }
}
