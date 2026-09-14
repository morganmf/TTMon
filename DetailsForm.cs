namespace TTMon;

// Male okienko na srodku ekranu pokazywane po podwojnym kliknieciu ikony w
// trayu - CPU/GPU/WAN, kazde jako: kropka + aktualna wartosc + wykres ostatnich
// 180 sekund (kolorowany gradientem tak samo jak ikona i kropka). Ma WLASNY
// timer i odswieza sie co sekunde, dopoki jest otwarte - dane pobiera na zywo
// przez delegaty, nie jednorazowa kopie z momentu otwarcia.
public sealed class DetailsForm : Form
{
    private const int ContentWidth = 260;
    private const int ChartHeight = 40;

    private readonly Func<SensorSnapshot> _snapshotProvider;
    private readonly Func<int?> _wanLatencyProvider;
    private readonly Func<IReadOnlyList<HistorySample>> _historyProvider;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 1000 };
    private readonly List<(Label Dot, Label Text, Func<string> BuildText, Func<Color> BuildColor)> _rows = new();
    private readonly List<SparklineChart> _charts = new();

    public DetailsForm(
        Func<SensorSnapshot> snapshotProvider,
        Func<int?> wanLatencyProvider,
        Func<IReadOnlyList<HistorySample>> historyProvider,
        AppSettings settings)
    {
        _snapshotProvider = snapshotProvider;
        _wanLatencyProvider = wanLatencyProvider;
        _historyProvider = historyProvider;
        _settings = settings;

        Text = Localization.T("details_title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = settings.DetailsAlwaysOnTop;

        // Przywroc zapamietana pozycje, jesli jest i wciaz miesci sie w
        // widocznych ekranach (np. user mogl odlaczyc drugi monitor od
        // ostatniego uruchomienia) - inaczej wyscentruj jak dotychczas.
        if (_settings.DetailsWindowX is int savedX && _settings.DetailsWindowY is int savedY &&
            Screen.AllScreens.Any(s => s.WorkingArea.Contains(savedX, savedY)))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(savedX, savedY);
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        ClientSize = new Size(ContentWidth + 30, 60); // tymczasowe, przeliczone na koniec

        int y = 20;

        if (_settings.ShowCpu)
        {
            AddMetricBlock(
                () => _snapshotProvider().CpuTempC is float c ? $"CPU: {c:0.0}\u00b0C" : "CPU: n/a",
                () => ColorForTemp(_snapshotProvider().CpuTempC),
                () => _historyProvider().Select(s => (s.Timestamp, s.CpuTempC)).ToList(),
                _settings.TempGradientMinC, _settings.TempGradientMaxC, "\u00b0C",
                ref y);
        }

        if (_settings.ShowGpu)
        {
            AddMetricBlock(
                () => _snapshotProvider().GpuTempC is float g ? $"GPU: {g:0.0}\u00b0C" : "GPU: n/a",
                () => ColorForTemp(_snapshotProvider().GpuTempC),
                () => _historyProvider().Select(s => (s.Timestamp, s.GpuTempC)).ToList(),
                _settings.TempGradientMinC, _settings.TempGradientMaxC, "\u00b0C",
                ref y);
        }

        if (_settings.ShowVrm)
        {
            AddMetricBlock(
                () => _snapshotProvider().VrmTempC is float v ? $"VRM: {v:0.0}\u00b0C" : "VRM: n/a",
                () => ColorForTemp(_snapshotProvider().VrmTempC),
                () => _historyProvider().Select(s => (s.Timestamp, s.VrmTempC)).ToList(),
                _settings.TempGradientMinC, _settings.TempGradientMaxC, "\u00b0C",
                ref y);
        }

        if (_settings.ShowWan)
        {
            AddMetricBlock(
                () => _wanLatencyProvider() is int ms ? $"WAN: {ms} ms" : $"WAN: {Localization.T("wan_offline")}",
                () =>
                {
                    var t = GradientPalette.Normalize(
                        _wanLatencyProvider() ?? _settings.WanGradientMinMs,
                        _settings.WanGradientMinMs, _settings.WanGradientMaxMs);
                    return GradientPalette.Sample(t);
                },
                () => _historyProvider().Select(s => (s.Timestamp, (float?)s.WanLatencyMs)).ToList(),
                _settings.WanGradientMinMs, _settings.WanGradientMaxMs, " ms",
                ref y);
        }

        y += 10;
        var closeBtn = new Button { Text = Localization.T("ok"), Left = (ContentWidth - 80) / 2 + 15, Top = y, Width = 80, DialogResult = DialogResult.OK };
        Controls.Add(closeBtn);
        AcceptButton = closeBtn;
        CancelButton = closeBtn;

        ClientSize = new Size(ContentWidth + 30, y + 50);

        RefreshValues(); // pierwsze wypelnienie od razu, bez czekania na pierwszy tick
        _refreshTimer.Tick += (_, _) => RefreshValues();
        _refreshTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();

        // Zapamietujemy pozycje dopiero na koniec (nie przy kazdym przesunieciu) -
        // Save() robi zapis na dysk, wiec robimy to raz, nie przy kazdym pikselu
        _settings.DetailsWindowX = Location.X;
        _settings.DetailsWindowY = Location.Y;
        _settings.Save();

        base.OnFormClosed(e);
    }

    private Color ColorForTemp(float? valueC)
    {
        var t = GradientPalette.Normalize(
            valueC ?? _settings.TempGradientMinC,
            _settings.TempGradientMinC, _settings.TempGradientMaxC);
        return GradientPalette.Sample(t);
    }

    private void AddMetricBlock(
        Func<string> textBuilder,
        Func<Color> colorBuilder,
        Func<List<(DateTime Timestamp, float? Value)>> chartDataBuilder,
        float minValue, float maxValue, string unit,
        ref int y)
    {
        var dot = new Label
        {
            Text = "\u25CF",
            Left = 15,
            Top = y,
            Width = 20,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        };
        var lbl = new Label
        {
            Left = 38,
            Top = y + 5,
            Width = ContentWidth - 23,
            Font = new Font("Segoe UI", 10f),
        };
        Controls.Add(dot);
        Controls.Add(lbl);
        _rows.Add((dot, lbl, textBuilder, colorBuilder));
        y += 26;

        var chart = new SparklineChart
        {
            Left = 15,
            Top = y,
            Width = ContentWidth,
            Height = ChartHeight,
            DataProvider = () => chartDataBuilder().Select(p => (p.Timestamp, p.Value)).ToList(),
            MinValue = minValue,
            MaxValue = maxValue,
            Unit = unit,
        };
        Controls.Add(chart);
        _charts.Add(chart);
        y += ChartHeight + 14;
    }

    private void RefreshValues()
    {
        foreach (var (dot, lbl, textBuilder, colorBuilder) in _rows)
        {
            lbl.Text = textBuilder();
            dot.ForeColor = colorBuilder();
        }

        foreach (var chart in _charts)
            chart.Invalidate();
    }
}
