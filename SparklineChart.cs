using System.Drawing.Drawing2D;

namespace TTMon;

// Mini wykres liniowy (sparkline) ostatnich 180 sekund - kazdy odcinek linii
// pokolorowany przez GradientPalette wg wartosci w tym miejscu (nie jeden
// staly kolor), tak samo jak ikona w trayu i kropki w DetailsForm. Odswieza
// sie przez Invalidate() wolane z zewnatrz (DetailsForm), nie ma wlasnego timera.
//
// Tooltip po najechaniu: przy kazdym OnPaint zapamietujemy realne wspolrzedne
// ekranowe narysowanych punktow (_lastPoints), zeby na MouseMove znalezc
// najblizszy bez ponownego przeliczania calej geometrii wykresu.
public sealed class SparklineChart : Panel
{
    public Func<IReadOnlyList<(DateTime Time, float? Value)>>? DataProvider { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }

    // Dla dowolnych czujnikow plyty glownej (napiecia, obroty wentylatorow,
    // rozne temperatury) nie znamy z gory sensownego sztywnego zakresu jak
    // przy CPU/GPU/WAN - z automatu liczymy min/max z tego, co faktycznie
    // jest w danych, zamiast polegac na MinValue/MaxValue powyzej.
    public bool AutoRange { get; set; }

    // Doklejane do wartosci w tooltipie, np. "\u00b0C" albo " ms"
    public string Unit { get; set; } = "";

    private readonly ToolTip _tooltip = new() { InitialDelay = 100, ReshowDelay = 50, AutoPopDelay = 8000 };
    private readonly List<(float X, float Y, DateTime Time, float Value)> _lastPoints = new();

    public SparklineChart()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(245, 245, 245);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        _lastPoints.Clear();

        if (Width <= 0 || Height <= 0) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        DrawGrid(g);

        var data = DataProvider?.Invoke();
        if (data == null || data.Count < 2)
            return;

        var now = DateTime.UtcNow;
        var windowSeconds = HistoryBuffer.Window.TotalSeconds;

        float effectiveMin = MinValue;
        float effectiveMax = MaxValue;
        if (AutoRange)
        {
            float? min = null, max = null;
            foreach (var (_, value) in data)
            {
                if (value is not float v) continue;
                if (min is null || v < min) min = v;
                if (max is null || v > max) max = v;
            }
            if (min is float mn && max is float mx)
            {
                effectiveMin = mn;
                effectiveMax = mx > mn ? mx : mn + 1f; // unikamy dzielenia przez zero
            }
        }

        PointF? prevPoint = null;
        float? prevValue = null;

        foreach (var (time, value) in data)
        {
            var elapsedSeconds = (now - time).TotalSeconds;
            var xFraction = 1f - (float)(elapsedSeconds / windowSeconds);
            xFraction = Math.Clamp(xFraction, 0f, 1f);
            var x = xFraction * Width;

            if (value is null)
            {
                // Przerwa w danych (np. sensor chwilowo niedostepny) - nie
                // laczymy linia przez dziure, zaczynamy nowy odcinek od nowa.
                prevPoint = null;
                prevValue = null;
                continue;
            }

            var t = GradientPalette.Normalize(value.Value, effectiveMin, effectiveMax);
            var y = Height - t * Height;
            var point = new PointF(x, y);

            if (prevPoint is PointF pp && prevValue is float pv)
            {
                var prevT = GradientPalette.Normalize(pv, effectiveMin, effectiveMax);
                var segmentColor = GradientPalette.Sample((t + prevT) / 2f);
                using var pen = new Pen(segmentColor, 2f);
                g.DrawLine(pen, pp, point);
            }

            _lastPoints.Add((x, y, time, value.Value));

            prevPoint = point;
            prevValue = value.Value;
        }
    }

    // Subtelna siatka w tle - 3 poziome linie (dziela wysokosc na czwiartki)
    // + pionowe co 30 sekund w oknie 180s. Rysowana ZAWSZE (nawet bez danych),
    // bo to tlo/uklad odniesienia, nie zalezy od tego czy juz jest co pokazac.
    private void DrawGrid(Graphics g)
    {
        using var gridPen = new Pen(Color.FromArgb(45, 128, 128, 128), 1f);

        const int horizontalLines = 3;
        for (int i = 1; i <= horizontalLines; i++)
        {
            var y = Height * i / (float)(horizontalLines + 1);
            g.DrawLine(gridPen, 0, y, Width, y);
        }

        const double intervalSeconds = 30;
        var windowSeconds = HistoryBuffer.Window.TotalSeconds;
        for (double t = intervalSeconds; t < windowSeconds; t += intervalSeconds)
        {
            var xFraction = 1f - (float)(t / windowSeconds);
            var x = xFraction * Width;
            g.DrawLine(gridPen, x, 0, x, Height);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_lastPoints.Count == 0)
        {
            _tooltip.Hide(this);
            return;
        }

        // Najblizszy punkt po osi X (czas plynie poziomo) - nie liczymy
        // odleglosci euklidesowej, bo interesuje nas "co bylo w tym momencie",
        // niezaleznie jak wysoko/nisko akurat lezala linia.
        var nearest = _lastPoints[0];
        var bestDistance = Math.Abs(nearest.X - e.X);
        foreach (var p in _lastPoints)
        {
            var d = Math.Abs(p.X - e.X);
            if (d < bestDistance)
            {
                bestDistance = d;
                nearest = p;
            }
        }

        if (bestDistance > 20)
        {
            _tooltip.Hide(this);
            return;
        }

        var secondsAgo = (int)(DateTime.UtcNow - nearest.Time).TotalSeconds;
        var text = $"{nearest.Value:0.0}{Unit}  \u2022  -{secondsAgo}s";
        _tooltip.Show(text, this, e.X + 14, e.Y - 24, 8000);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _tooltip.Hide(this);
    }
}
