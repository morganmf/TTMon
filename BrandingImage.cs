using System.Reflection;

namespace TTMon;

// Wczytuje obrazek splash/Info z zasobu OSADZONEGO w binarce (nie z osobnego
// pliku na dysku) - jeden .exe, nic do zgubienia przy przenoszeniu/kopiowaniu.
// Wczytany raz i trzymany w pamieci, uzywany zarowno przez SplashForm jak i InfoForm.
public static class BrandingImage
{
    private static Image? _cached;
    private static bool _attempted;

    public static Image? Load()
    {
        if (_attempted) return _cached;
        _attempted = true;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Nazwa zasobu zalezy od RootNamespace projektu (np.
            // "TTMon.Resources.splash.jpg") - szukamy po sufiksie zamiast
            // liczyc na dokladna nazwe, zeby nie polegac na konkretnym namespace.
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("splash.jpg", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    // Kopiujemy do wlasnego MemoryStream - Image.FromStream wymaga
                    // zeby zrodlowy strumien zyl tak dlugo jak obrazek, a resource
                    // stream zamykamy zaraz po odczycie (using powyzej)
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    buffer.Position = 0;
                    _cached = Image.FromStream(buffer);
                }
            }
        }
        catch
        {
            // Brak zasobu (np. cos poszlo nie tak przy buildzie) - splash/Info
            // po prostu wtedy nie pokaza obrazka, bez wywalania appki
        }

        return _cached;
    }
}
