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

        var data = DataProvider?.Invoke();
        if (data == null || data.Count < 2 || Width <= 0 || Height <= 0)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var now = DateTime.UtcNow;
        var windowSeconds = HistoryBuffer.Window.TotalSeconds;

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

            var t = GradientPalette.Normalize(value.Value, MinValue, MaxValue);
            var y = Height - t * Height;
            var point = new PointF(x, y);

            if (prevPoint is PointF pp && prevValue is float pv)
            {
                var prevT = GradientPalette.Normalize(pv, MinValue, MaxValue);
                var segmentColor = GradientPalette.Sample((t + prevT) / 2f);
                using var pen = new Pen(segmentColor, 2f);
                g.DrawLine(pen, pp, point);
            }

            _lastPoints.Add((x, y, time, value.Value));

            prevPoint = point;
            prevValue = value.Value;
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
