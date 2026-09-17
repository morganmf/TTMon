namespace TTMon;

// Lista wszystkich czujnikow plyty glownej (temperatury, wentylatory, napiecia)
// z wykresem wybranego wiersza na dole - wzorowane na zakladce Sensors w GPU-Z:
// klikasz wiersz, wykres pod spodem pokazuje historie TEGO konkretnego czujnika.
// Kazdy typ czujnika ma inna skale (V/RPM/C) wiec wykres uzywa AutoRange
// (SparklineChart), nie sztywnego zakresu jak przy CPU/GPU/WAN.
//
// Layout NIE korzysta z systemu Anchor dla ukladu pionowego (tylko Top/Left) -
// zamiast tego PerformCustomLayout() jawnie przelicza pozycje przy kazdej
// zmianie rozmiaru okna. Powod: przy Anchor=Bottom na liscie i osobnych,
// sztywnych wspolrzednych Top na etykiecie/wykresie ponizej, powiekszenie okna
// powodowalo ze lista rosla i fizycznie zachodzila na wykres pod spodem (bo
// "odleglosc od dolu formularza" byla zachowywana niezaleznie dla kazdej
// kontrolki z osobna, nie jako spojny, sekwencyjny uklad).
public sealed class SensorsForm : Form
{
    private const int ContentWidth = 380;
    private const int ChartHeight = 110;
    private const int ChartLabelHeight = 20;
    private const int ButtonAreaHeight = 45;
    private const int EdgeMargin = 15;
    private const int Gap = 10;

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
        MinimumSize = new Size(ContentWidth + 40, 420);
        ClientSize = new Size(ContentWidth + 30, 480);

        _listView.Columns.Add(Localization.T("sensors_col_name"), 220);
        _listView.Columns.Add(Localization.T("sensors_col_value"), 130);
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

        _chartLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _chartLabel.Text = Localization.T("sensors_select_hint");
        Controls.Add(_chartLabel);

        _chart.DataProvider = BuildChartData;
        Controls.Add(_chart);

        _closeBtn.Text = Localization.T("ok");
        _closeBtn.Width = 80;
        _closeBtn.DialogResult = DialogResult.OK;
        Controls.Add(_closeBtn);
        AcceptButton = _closeBtn;
        CancelButton = _closeBtn;

        PerformCustomLayout();
        Resize += (_, _) => PerformCustomLayout();

        RefreshList(selectFirstIfNone: true);
        _refreshTimer.Tick += (_, _) => RefreshList(selectFirstIfNone: false);
        _refreshTimer.Start();

        Load += (_, _) => ThemeHelper.Apply(this, _settings.DarkMode);
    }

    // Jedyne miejsce ktore ustawia pozycje/rozmiary kontrolek - wolane raz na
    // starcie i przy kazdej zmianie rozmiaru okna (Resize). Lista wypelnia
    // cala dostepna przestrzen ponad wykresem, wykres i przycisk maja stala
    // wysokosc, wiec nic nigdy na siebie nie zachodzi niezaleznie od rozmiaru okna.
    private void PerformCustomLayout()
    {
        var width = Math.Max(100, ClientSize.Width - EdgeMargin * 2);

        var chartTop = ClientSize.Height - ButtonAreaHeight - ChartHeight;
        var chartLabelTop = chartTop - Gap - ChartLabelHeight;
        var listViewHeight = Math.Max(80, chartLabelTop - Gap - EdgeMargin);

        _listView.Left = EdgeMargin;
        _listView.Top = EdgeMargin;
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

    private void RefreshList(bool selectFirstIfNone)
    {
        var sensors = _liveSensorsProvider();

        if (_listView.Items.Count != sensors.Count)
        {
            // Zestaw czujnikow sie zmienil (rzadki przypadek) - dopiero wtedy
            // budujemy liste od zera.
            RebuildList(sensors);
        }
        else
        {
            // Normalny, powtarzajacy sie co sekunde przypadek: TYLKO
            // aktualizujemy tekst wartosci w istniejacych wierszach, bez
            // Clear()+Add(). To jest kluczowe - Clear() resetuje przewijanie
            // (TopItem) i wymusza od nowa caly proces zaznaczania w WinForms
            // ListView, wiec user nigdy nie zdazylby przewinac listy w dol,
            // bo za ulamek sekundy i tak wracalaby na gore.
            for (int i = 0; i < sensors.Count; i++)
            {
                var s = sensors[i];
                var valueText = s.Value is float v ? $"{v:0.0} {s.Unit}" : Localization.T("info_unknown");
                _listView.Items[i].SubItems[1].Text = valueText;
            }
        }

        if (_listView.SelectedItems.Count == 0 && selectFirstIfNone && _listView.Items.Count > 0)
            _listView.Items[0].Selected = true;

        _chart.Invalidate();
    }

    private void RebuildList(List<(string Name, float? Value, string Unit)> sensors)
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
            var valueText = s.Value is float v ? $"{v:0.0} {s.Unit}" : Localization.T("info_unknown");
            var item = new ListViewItem(new[] { key, valueText }) { Tag = key };
            _listView.Items.Add(item);

            if (key == previouslySelected)
                item.Selected = true;
        }
        _listView.EndUpdate();
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
