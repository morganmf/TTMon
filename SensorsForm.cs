namespace TTMon;

// Lista wszystkich czujnikow plyty glownej (temperatury, wentylatory, napiecia)
// z wykresem wybranego wiersza na dole - wzorowane na zakladce Sensors w GPU-Z:
// klikasz wiersz, wykres pod spodem pokazuje historie TEGO konkretnego czujnika.
// Kazdy typ czujnika ma inna skale (V/RPM/C) wiec wykres uzywa AutoRange
// (SparklineChart), nie sztywnego zakresu jak przy CPU/GPU/WAN.
public sealed class SensorsForm : Form
{
    private const int ContentWidth = 380;

    private readonly Func<List<(string Name, float? Value, string Unit)>> _liveSensorsProvider;
    private readonly Func<IReadOnlyList<HistorySample>> _historyProvider;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 1000 };

    private readonly ListView _listView = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
    };
    private readonly SparklineChart _chart = new() { AutoRange = true };
    private readonly Label _chartLabel = new();

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
        MinimumSize = new Size(ContentWidth + 40, 420);
        ClientSize = new Size(ContentWidth + 30, 480);

        _listView.Columns.Add(Localization.T("sensors_col_name"), 220);
        _listView.Columns.Add(Localization.T("sensors_col_value"), 130);
        _listView.Left = 15;
        _listView.Top = 15;
        _listView.Width = ContentWidth;
        _listView.Height = 260;
        _listView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _listView.SelectedIndexChanged += (_, _) =>
        {
            if (_listView.SelectedItems.Count == 0) return;
            var item = _listView.SelectedItems[0];
            _selectedSensorName = item.Tag as string;
            _chartLabel.Text = item.Text;
            _chart.Unit = " " + (item.SubItems.Count > 1 ? ExtractUnit(item.SubItems[1].Text) : "");
            _chart.Invalidate();
        };
        Controls.Add(_listView);

        _chartLabel.Left = 15;
        _chartLabel.Top = 285;
        _chartLabel.Width = ContentWidth;
        _chartLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _chartLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _chartLabel.Text = Localization.T("sensors_select_hint");
        Controls.Add(_chartLabel);

        _chart.Left = 15;
        _chart.Top = 308;
        _chart.Width = ContentWidth;
        _chart.Height = 110;
        _chart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _chart.DataProvider = BuildChartData;
        Controls.Add(_chart);

        var closeBtn = new Button
        {
            Text = Localization.T("ok"),
            Width = 80,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
        };
        closeBtn.Left = ContentWidth + 15 - closeBtn.Width;
        closeBtn.Top = 428;
        Controls.Add(closeBtn);
        AcceptButton = closeBtn;
        CancelButton = closeBtn;

        RefreshList(selectFirstIfNone: true);
        _refreshTimer.Tick += (_, _) => RefreshList(selectFirstIfNone: false);
        _refreshTimer.Start();

        Load += (_, _) => ThemeHelper.Apply(this, _settings.DarkMode);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void RefreshList(bool selectFirstIfNone)
    {
        var sensors = _liveSensorsProvider();
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
            var valueText = s.Value is float v ? $"{v:0.0} {s.Unit}" : Localization.T("info_unknown");
            var item = new ListViewItem(new[] { key, valueText }) { Tag = key };
            _listView.Items.Add(item);

            if (key == previouslySelected)
                item.Selected = true;
        }
        _listView.EndUpdate();

        if (_listView.SelectedItems.Count == 0 && selectFirstIfNone && _listView.Items.Count > 0)
            _listView.Items[0].Selected = true;

        _chart.Invalidate();
    }

    private List<(DateTime Time, float? Value)> BuildChartData()
    {
        if (_selectedSensorName == null) return new List<(DateTime, float?)>();

        return _historyProvider()
            .Select(sample => (sample.Timestamp,
                sample.MotherboardSensors.TryGetValue(_selectedSensorName, out var v) ? (float?)v : null))
            .ToList();
    }

    private static string ExtractUnit(string valueText)
    {
        // "42.0 RPM" -> "RPM" - proste wyciagniecie jednostki z ostatniego slowa
        var parts = valueText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : "";
    }
}
