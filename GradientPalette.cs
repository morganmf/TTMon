namespace TTMon;

// Jeden uniwersalny gradient (nie zalezny od motywu Windows - patrz
// TrayIconRenderer, kontrast zapewnia ciemna podkladka pod tekstem, nie
// osobny zestaw kolorow per motyw): chlodny (niebieski) -> zielony ->
// pomaranczowy (letni) -> czerwony -> purpurowy (gorący).
public static class GradientPalette
{
    private static readonly (float Pos, Color Color)[] Stops =
    {
        (0.00f, ColorTranslator.FromHtml("#2196F3")), // chlodny - niebieski
        (0.25f, ColorTranslator.FromHtml("#4CAF50")), // zielony
        (0.50f, ColorTranslator.FromHtml("#FF9800")), // pomaranczowy
        (0.75f, ColorTranslator.FromHtml("#F44336")), // czerwony
        (1.00f, ColorTranslator.FromHtml("#9C27B0")), // purpurowy
    };

    // t w zakresie 0..1 - 0 = najzimniej, 1 = najgoreciej
    public static Color Sample(float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        for (int i = 0; i < Stops.Length - 1; i++)
        {
            var (posA, colorA) = Stops[i];
            var (posB, colorB) = Stops[i + 1];
            if (t >= posA && t <= posB)
            {
                float span = posB - posA;
                float localT = span < 0.0001f ? 0f : (t - posA) / span;
                return Lerp(colorA, colorB, localT);
            }
        }
        return Stops[^1].Color;
    }

    // Normalizuje wartosc (np. temperature albo ms) do zakresu 0..1 na podstawie
    // ustawionych przez usera progow min/max gradientu.
    public static float Normalize(float value, float min, float max)
    {
        if (max <= min) return 0f;
        return Math.Clamp((value - min) / (max - min), 0f, 1f);
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        int r = (int)Math.Round(a.R + (b.R - a.R) * t);
        int g = (int)Math.Round(a.G + (b.G - a.G) * t);
        int bl = (int)Math.Round(a.B + (b.B - a.B) * t);
        return Color.FromArgb(r, g, bl);
    }
}
