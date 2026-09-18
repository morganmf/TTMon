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

    public static Icon Render(string text, Color color, IconSizeLevel sizeLevel, TrayFontChoice fontChoice, bool outline, bool backgroundPlate, IconColorMode colorMode)
    {
        if (colorMode == IconColorMode.ColoredBackground)
            return RenderColoredBackground(text, color, sizeLevel, fontChoice);

        return RenderColoredText(text, color, sizeLevel, fontChoice, outline, backgroundPlate);
    }

    private static Icon RenderColoredText(string text, Color color, IconSizeLevel sizeLevel, TrayFontChoice fontChoice, bool outline, bool backgroundPlate)
    {
        using var bmp = new Bitmap(SourceSizePx, SourceSizePx);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var (desiredScale, capScale) = ScaleFor(sizeLevel);
            // Kontur (biala "aureola") i jasne tlo oba wystaja poza sam obrys
            // liter - rezerwujemy na to margines w limicie, zeby nic sie nie
            // ucielo na krawedzi 64px bitmapy.
            var reserve = (outline ? SourceSizePx * 0.09f : 0f) + (backgroundPlate ? SourceSizePx * 0.06f : 0f);
            var maxDimension = SourceSizePx * capScale - reserve;
            var desiredSize = SourceSizePx * desiredScale;
            var family = ResolveFontFamily(fontChoice);
            var style = ResolveStyle(family);

            using var path = BuildFittingGlyphPath(text, family, style, desiredSize, maxDimension, maxDimension, out var bounds);

            var translateX = (SourceSizePx - bounds.Width) / 2f - bounds.X;
            var translateY = (SourceSizePx - bounds.Height) / 2f - bounds.Y;
            using var matrix = new Matrix();
            matrix.Translate(translateX, translateY);
            path.Transform(matrix);

            // Jasne, PELNE (wypelnione, nie tylko linia) tlo pod cyframi.
            // Powod istnienia tej opcji: renderujemy w wysokiej rozdzielczosci
            // (64px), ale finalny rozmiar w trayu jest maleki - cienkie linie
            // (nawet kontur ponizej) po takim zmniejszeniu tracaja wiekszosc
            // efektu przez antyaliasing. Pelny, wypelniony ksztalt przetrwa
            // pomniejszenie duzo lepiej, bo to spory blok koloru, nie cienka
            // kreska - i eliminuje caly problem "raz jasne, raz ciemne tlo"
            // pod spodem, bo staje sie JEDYNYM tlem jakie cyfry widza.
            if (backgroundPlate)
            {
                var finalBounds = path.GetBounds();
                var pad = SourceSizePx * 0.09f;
                var plateRect = RectangleF.Inflate(finalBounds, pad, pad);
                plateRect.Intersect(new RectangleF(0, 0, SourceSizePx, SourceSizePx));

                using var platePath = RoundedRect(plateRect, SourceSizePx * 0.16f);
                using var plateBrush = new SolidBrush(Color.FromArgb(230, 250, 250, 250));
                g.FillPath(plateBrush, platePath);
            }

            // PODWOJNY kontur (biala "aureola" na zewnatrz + czarny pierscien
            // do wewnatrz) - dziala niezaleznie od tla, wiec zostaje nawet
            // gdy backgroundPlate jest wlaczone (dodatkowa ostrosc krawedzi).
            if (outline)
            {
                using var whitePen = new Pen(Color.FromArgb(235, 255, 255, 255), SourceSizePx * 0.085f)
                {
                    LineJoin = LineJoin.Round,
                };
                g.DrawPath(whitePen, path);

                using var blackPen = new Pen(Color.FromArgb(220, 0, 0, 0), SourceSizePx * 0.045f)
                {
                    LineJoin = LineJoin.Round,
                };
                g.DrawPath(blackPen, path);
            }

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        nint hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    // Tryb "kolorowe tlo": zamiast walczyc z nieznanym tlem paska zadan, SAMI
    // definiujemy jedyne tlo jakie sie liczy - plyta wypelniona kolorem z
    // gradientu, a tekst dobiera sie automatycznie na czarny/bialy w
    // zaleznosci od jasnosci TEJ plyty (luminancja Rec. 709), wiec zawsze
    // kontrastuje z gwarancja, bez zgadywania co jest pod spodem na pulpicie.
    private static Icon RenderColoredBackground(string text, Color bgColor, IconSizeLevel sizeLevel, TrayFontChoice fontChoice)
    {
        using var bmp = new Bitmap(SourceSizePx, SourceSizePx);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var plateRect = new RectangleF(2, 2, SourceSizePx - 4, SourceSizePx - 4);
            using (var platePath = RoundedRect(plateRect, SourceSizePx * 0.22f))
            using (var plateBrush = new SolidBrush(bgColor))
            {
                g.FillPath(plateBrush, platePath);
            }

            var textColor = IsLightColor(bgColor) ? Color.Black : Color.White;

            var (desiredScale, _) = ScaleFor(sizeLevel);
            // Plyta ma stalы, przewidywalny rozmiar (nie zalezy od obrysu
            // tekstu jak backgroundPlate w trybie ColoredText) - liczymy
            // limit tekstu wzgledem NIEJ, z bezpiecznym marginesem od jej
            // wlasnych krawedzi.
            var maxDimension = plateRect.Width * 0.82f;
            var desiredSize = SourceSizePx * desiredScale;
            var family = ResolveFontFamily(fontChoice);
            var style = ResolveStyle(family);

            using var path = BuildFittingGlyphPath(text, family, style, desiredSize, maxDimension, maxDimension, out var bounds);

            var translateX = (SourceSizePx - bounds.Width) / 2f - bounds.X;
            var translateY = (SourceSizePx - bounds.Height) / 2f - bounds.Y;
            using var matrix = new Matrix();
            matrix.Translate(translateX, translateY);
            path.Transform(matrix);

            using var brush = new SolidBrush(textColor);
            g.FillPath(brush, path);
        }

        nint hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    // Standardowa percepcyjna luminancja (Rec. 709) - dokladniejsza niz prosta
    // srednia R+G+B, bo oko ludzkie jest dużo bardziej czule na zielony niz
    // na niebieski, co ta formula uwzglednia.
    private static bool IsLightColor(Color c)
    {
        var luminance = (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) / 255f;
        return luminance > 0.55f;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (d <= 0f)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
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
                TrayFontChoice.FiraCodeMono => EmbeddedFontLoader.Load("FiraCodeNerdFontMono-Retina.ttf") ?? new FontFamily(DefaultFontFamilyName),
                TrayFontChoice.EnvyCodeRMono => EmbeddedFontLoader.Load("EnvyCodeRNerdFontMono-Regular.ttf") ?? new FontFamily(DefaultFontFamilyName),
                TrayFontChoice.TerminessMono => EmbeddedFontLoader.Load("TerminessNerdFontMono-Regular.ttf") ?? new FontFamily(DefaultFontFamilyName),
                TrayFontChoice.MesloLGLMono => EmbeddedFontLoader.Load("MesloLGLNerdFontMono-Regular.ttf") ?? new FontFamily(DefaultFontFamilyName),
                // Hurmit to .otf z konturami CFF/PostScript (nie TrueType jak
                // reszta) - GDI+ (uzywane tu do renderowania) bywa z tym
                // formatem niepewne. Jesli EmbeddedFontLoader.Load zwroci null
                // (nie zaladuje sie poprawnie), cicho spadamy do Segoe UI -
                // ten sam mechanizm bezpieczenstwa co dla brakujacych plikow.
                TrayFontChoice.HurmitMono => EmbeddedFontLoader.Load("HurmitNerdFontMono-Regular.otf") ?? new FontFamily(DefaultFontFamilyName),
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
