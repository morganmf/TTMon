namespace TTMon;

// Lista wszystkich czujnikow plyty glownej (temperatury, wentylatory, napiecia)
// z wykresem wybranego wiersza na dole - wzorowane na zakladce Sensors w GPU-Z:
// klikasz wiersz, wykres pod spodem pokazuje historie TEGO konkretnego czujnika.
// Kazdy typ czujnika ma inna skale (V/RPM/C) wiec wykres uzywa AutoRange
// (SparklineChart), nie sztywnego zakresu jak przy CPU/GPU/WAN.
//
// Layout NIE korzysta z systemu Anchor dla ukladu pionowego (tylko Top/Left) -
// zamiast tego PerformCustomLayout() jawnie przelicza pozycje przy kazdej
// zmianie rozmiaru okna.
public sealed class SensorsForm : Form
{
    private const int ContentWidth = 400;
    private const int ChartHeight = 110;
    private const int ChartLabelHeight = 20;
    private const int ButtonAreaHeight = 45;
    private const int FilterRowHeight = 24;
    private const int EdgeMargin = 15;
    private const int Gap = 10;

    private readonly Func<List<(string Name, float? Value, string Unit)>> _liveSensorsProvider;
    private readonly Func<IReadOnlyList<HistorySample>> _historyProvider;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 1000 };

    private readonly CheckBox _hideInactiveBox = new();
    private readonly ListView _listView = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
    };
    private readonly SparklineChart _chart = new() { AutoRange = true };
    private readonly Label _chartLabel = new();
    private readonly Button _closeBtn = new();

    private string? _selectedSensorName;

    public SensorsForm(
        Func<List<(string Name, float? Value, string Unit)>> liveSensorsProvider,
        Func<IReadOnlyList<HistorySample>> historyProvider,
        AppSettings settings)
    {
        _liveSensorsProvider = liveSensorsProvider;
        _historyProvider = historyProvider;
        _settings = settings;

        Text = Localization.T("sensors_title");
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(ContentWidth + 40, 440);
        ClientSize = new Size(ContentWidth + 30, 500);

        _hideInactiveBox.Text = Localization.T("sensors_hide_inactive");
        _hideInactiveBox.CheckedChanged += (_, _) => RefreshList(selectFirstIfNone: false, forceRebuild: true);
        Controls.Add(_hideInactiveBox);

        _listView.Columns.Add(Localization.T("sensors_col_name"), 150);
        _listView.Columns.Add(Localization.T("sensors_col_value"), 65);
        _listView.Columns.Add(Localization.T("sensors_col_min"), 55);
        _listView.Columns.Add(Localization.T("sensors_col_max"), 55);
        _listView.Columns.Add(Localization.T("sensors_col_avg"), 55);
        _listView.SelectedIndexChanged += (_, _) =>
        {
            if (_listView.SelectedItems.Count == 0) return;
            var item = _listView.SelectedItems[0];
            _selectedSensorName = item.Tag as string;
            _chartLabel.Text = item.Text;
            _chart.Unit = " " + ExtractUnitFromKey(_selectedSensorName ?? "");
            _chart.Invalidate();
        };
        Controls.Add(_listView);

        _chartLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _chartLabel.Text = Localization.T("sensors_select_hint");
        Controls.Add(_chartLabel);

        _chart.DataProvider = BuildChartData;
        Controls.Add(_chart);

        _closeBtn.Text = Localization.T("close");
        _closeBtn.Width = 80;
        _closeBtn.DialogResult = DialogResult.OK;
        _closeBtn.Click += (_, _) => Close();
        Controls.Add(_closeBtn);
        AcceptButton = _closeBtn;
        CancelButton = _closeBtn;

        PerformCustomLayout();
        Resize += (_, _) => PerformCustomLayout();

        RefreshList(selectFirstIfNone: true, forceRebuild: true);
        _refreshTimer.Tick += (_, _) => RefreshList(selectFirstIfNone: false, forceRebuild: false);
        _refreshTimer.Start();

        Icon = AppIconLoader.Load() ?? Icon;
        Load += (_, _) => ThemeHelper.Apply(this, _settings.DarkMode);
    }

    private void PerformCustomLayout()
    {
        var width = Math.Max(100, ClientSize.Width - EdgeMargin * 2);

        _hideInactiveBox.Left = EdgeMargin;
        _hideInactiveBox.Top = EdgeMargin;
        _hideInactiveBox.Width = width;
        _hideInactiveBox.Height = FilterRowHeight;

        var listViewTop = EdgeMargin + FilterRowHeight + 4;
        var chartTop = ClientSize.Height - ButtonAreaHeight - ChartHeight;
        var chartLabelTop = chartTop - Gap - ChartLabelHeight;
        var listViewHeight = Math.Max(80, chartLabelTop - Gap - listViewTop);

        _listView.Left = EdgeMargin;
        _listView.Top = listViewTop;
        _listView.Width = width;
        _listView.Height = listViewHeight;

        _chartLabel.Left = EdgeMargin;
        _chartLabel.Top = chartLabelTop;
        _chartLabel.Width = width;
        _chartLabel.Height = ChartLabelHeight;

        _chart.Left = EdgeMargin;
        _chart.Top = chartTop;
        _chart.Width = width;
        _chart.Height = ChartHeight;

        _closeBtn.Left = ClientSize.Width - EdgeMargin - _closeBtn.Width;
        _closeBtn.Top = ClientSize.Height - ButtonAreaHeight + 5;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void RefreshList(bool selectFirstIfNone, bool forceRebuild)
    {
        var allSensors = _liveSensorsProvider();
        // "Nieaktywny" = wartosc zerowa (np. wentylator ktory sie nie krecil,
        // "Pump Fan #1: 0.0 RPM" na plytach gdzie taka pompa nie jest w ogole
        // podpieta) - nie null (null = "nie wykryto w ogole", inna kategoria,
        // te zawsze zostaja widoczne zeby bylo widac ze czujnik istnieje).
        var visible = _hideInactiveBox.Checked
            ? allSensors.Where(s => s.Value is not float v || Math.Abs(v) > 0.01f).ToList()
            : allSensors;

        var history = _historyProvider();

        if (forceRebuild || _listView.Items.Count != visible.Count)
        {
            RebuildList(visible, history);
        }
        else
        {
            for (int i = 0; i < visible.Count; i++)
            {
                var s = visible[i];
                var key = $"{s.Name} ({s.Unit})";
                var (min, max, avg) = ComputeStats(history, key);
                var item = _listView.Items[i];
                item.SubItems[1].Text = FormatValue(s.Value);
                item.SubItems[2].Text = FormatValue(min);
                item.SubItems[3].Text = FormatValue(max);
                item.SubItems[4].Text = FormatValue(avg);
            }
        }

        if (_listView.SelectedItems.Count == 0 && selectFirstIfNone && _listView.Items.Count > 0)
            _listView.Items[0].Selected = true;

        _chart.Invalidate();
    }

    private void RebuildList(List<(string Name, float? Value, string Unit)> sensors, IReadOnlyList<HistorySample> history)
    {
        var previouslySelected = _selectedSensorName;

        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var s in sensors)
        {
            // Klucz nazwa+jednostka (nie sama nazwa) - niektore plyty maja
            // wiecej niz jeden sensor o tej samej nazwie (np. "Chipset" jako
            // napiecie ORAZ jako osobna temperatura). Musi sie zgadzac z
            // kluczem budowanym w TrayApplicationContext.RefreshAsync, zeby
            // wykres historii trafil do wlasciwego czujnika.
            var key = $"{s.Name} ({s.Unit})";
            var (min, max, avg) = ComputeStats(history, key);
            var item = new ListViewItem(new[]
            {
                key,
                FormatValue(s.Value),
                FormatValue(min),
                FormatValue(max),
                FormatValue(avg),
            })
            { Tag = key };
            _listView.Items.Add(item);

            if (key == previouslySelected)
                item.Selected = true;
        }
        _listView.EndUpdate();
    }

    private static string FormatValue(float? value) =>
        value is float v ? $"{v:0.0}" : Localization.T("info_unknown");

    private static (float? Min, float? Max, float? Avg) ComputeStats(IReadOnlyList<HistorySample> history, string key)
    {
        float? min = null, max = null;
        double sum = 0;
        int count = 0;

        foreach (var sample in history)
        {
            if (!sample.MotherboardSensors.TryGetValue(key, out var v)) continue;
            if (min is null || v < min) min = v;
            if (max is null || v > max) max = v;
            sum += v;
            count++;
        }

        return (min, max, count > 0 ? (float)(sum / count) : null);
    }

    private List<(DateTime Time, float? Value)> BuildChartData()
    {
        if (_selectedSensorName == null) return new List<(DateTime, float?)>();

        return _historyProvider()
            .Select(sample => (sample.Timestamp,
                sample.MotherboardSensors.TryGetValue(_selectedSensorName, out var v) ? (float?)v : null))
            .ToList();
    }

    // Klucz ma zawsze format "Nazwa (Jednostka)" - wyciagamy tekst w nawiasie
    private static string ExtractUnitFromKey(string key)
    {
        var start = key.LastIndexOf('(');
        var end = key.LastIndexOf(')');
        return start >= 0 && end > start ? key[(start + 1)..end] : "";
    }
}
