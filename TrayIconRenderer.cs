using System.Drawing.Drawing2D;

namespace TTMon;

// Generuje w locie ikone z liczba (temperatura lub ms) i kolorem z gradientu.
//
// UWAGA (wazne ograniczenie Windows): rozmiaru ikony w trayu NIE da sie
// kontrolowac z poziomu aplikacji - system przydziela kazdej ikonie staly,
// systemowy rozmiar slotu, i zawsze przeskaluje nasza bitmape do tego rozmiaru.
// Renderujemy wiec zawsze w wysokiej rozdzielczosci zrodlowej (64x64), a
// IconSizeLevel reguluje jak duzo miejsca w OBREBIE tego stalego kwadratu
// zajmuje tekst. Centrowanie i pomiar ida przez PRAWDZIWY geometryczny obrys
// (GraphicsPath.GetBounds), nie przez metryki czcionki - patrz komentarz w
// BuildFittingGlyphPath.
public static class TrayIconRenderer
{
    private const int SourceSizePx = 64;
    private const string DefaultFontFamilyName = "Segoe UI";

    // Cache rodzin czcionek - tworzone raz na wybor, nie przy kazdym renderze
    // (Render() jest wolany co kilka sekund przy kazdym odswiezeniu).
    private static readonly Dictionary<TrayFontChoice, FontFamily> FamilyCache = new();

    // Kazdy poziom ma wlasny docelowy rozmiar i wlasny sufit bezpieczenstwa
    // (dla dlugich tekstow typu "104") - patrz historia w README/rozmowie,
    // dlaczego to musi byc per-poziom, a nie jeden wspolny limit.
    // +2px na kazdym poziomie, druga runda (+0.03125 = 2/64) na zyczenie.
    private static (float Desired, float Cap) ScaleFor(IconSizeLevel level) => level switch
    {
        IconSizeLevel.Small => (0.46f, 0.61f),
        IconSizeLevel.Large => (0.84f, 0.98f),
        _ => (0.64f, 0.81f), // Medium
    };

    public static Icon Render(string text, Color color, IconSizeLevel sizeLevel, TrayFontChoice fontChoice, bool outline)
    {
        using var bmp = new Bitmap(SourceSizePx, SourceSizePx);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var (desiredScale, capScale) = ScaleFor(sizeLevel);
            var maxDimension = SourceSizePx * capScale;
            var desiredSize = SourceSizePx * desiredScale;
            var family = ResolveFontFamily(fontChoice);
            var style = ResolveStyle(family);

            using var path = BuildFittingGlyphPath(text, family, style, desiredSize, maxDimension, maxDimension, out var bounds);

            var translateX = (SourceSizePx - bounds.Width) / 2f - bounds.X;
            var translateY = (SourceSizePx - bounds.Height) / 2f - bounds.Y;
            using var matrix = new Matrix();
            matrix.Translate(translateX, translateY);
            path.Transform(matrix);

            // Kontur pod spodem (czarny, polprzezroczysty) - dopiero na to
            // kolorowy wypelnienie. Pomaga na paskach zadan z wlaczona
            // przezroczystoscia/jasnym tlem, gdzie same nasycone kolory
            // gradientu czasem gina w tle.
            if (outline)
            {
                using var outlinePen = new Pen(Color.FromArgb(200, 0, 0, 0), SourceSizePx * 0.045f)
                {
                    LineJoin = LineJoin.Round,
                };
                g.DrawPath(outlinePen, path);
            }

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        nint hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static FontFamily ResolveFontFamily(TrayFontChoice choice)
    {
        if (FamilyCache.TryGetValue(choice, out var cached)) return cached;

        FontFamily family;
        try
        {
            family = choice switch
            {
                // Bahnschrift jest wbudowany w Windows 10/11 (od wersji 1709) -
                // jesli z jakiegos powodu go nie ma, lapiemy wyjatek i wracamy
                // do Segoe UI.
                TrayFontChoice.Bahnschrift => new FontFamily("Bahnschrift"),
                // Dosis/JetBrainsMono NIE sa czcionkami systemowymi - wymagaja
                // dostarczonych przez usera plikow w Resources/ (patrz
                // EmbeddedFontLoader). Jesli pliku nie ma, Load() zwraca null
                // i cicho wracamy do Segoe UI.
                TrayFontChoice.Dosis => EmbeddedFontLoader.Load("Dosis.ttf") ?? new FontFamily(DefaultFontFamilyName),
                TrayFontChoice.JetBrainsMono => EmbeddedFontLoader.Load("JetBrainsMono-Bold.ttf") ?? new FontFamily(DefaultFontFamilyName),
                _ => new FontFamily(DefaultFontFamilyName),
            };
        }
        catch
        {
            family = new FontFamily(DefaultFontFamilyName);
        }

        FamilyCache[choice] = family;
        return family;
    }

    // Bahnschrift (font wariantowy) i potencjalnie inne dostarczone przez usera
    // czcionki moga NIE obslugiwac Bold - GraphicsPath.AddString rzucalby
    // wyjatek gdyby zadany styl nie byl dostepny, wiec sprawdzamy z gory.
    private static FontStyle ResolveStyle(FontFamily family)
    {
        if (family.IsStyleAvailable(FontStyle.Bold)) return FontStyle.Bold;
        return FontStyle.Regular;
    }

    // Zaczyna od pozadanego rozmiaru i zmniejsza az PRAWDZIWY obrys tekstu
    // (nie metryki czcionki) zmiesci sie w limicie.
    private static GraphicsPath BuildFittingGlyphPath(string text, FontFamily family, FontStyle style, float desiredSizePx, float maxWidth, float maxHeight, out RectangleF bounds)
    {
        float size = desiredSizePx;

        while (size > 6f)
        {
            var path = new GraphicsPath();
            path.AddString(text, family, (int)style, size, PointF.Empty, StringFormat.GenericTypographic);
            var candidateBounds = path.GetBounds();

            if (candidateBounds.Width <= maxWidth && candidateBounds.Height <= maxHeight)
            {
                bounds = candidateBounds;
                return path;
            }

            path.Dispose();
            size -= 1f;
        }

        var fallback = new GraphicsPath();
        fallback.AddString(text, family, (int)style, 6f, PointF.Empty, StringFormat.GenericTypographic);
        bounds = fallback.GetBounds();
        return fallback;
    }
}
