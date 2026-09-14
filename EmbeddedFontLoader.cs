using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TTMon;

// Wczytuje dowolna czcionke osadzona jako EmbeddedResource (np. Resources/Dosis.ttf)
// jako PrivateFontCollection - wywolujacy podaje sama nazwe pliku, szukamy
// zasobu ktory sie na nia konczy (niezaleznie od dokladnej sciezki namespace'u).
// Wynik trzymany w cache per plik, zeby nie wczytywac wielokrotnie. Jesli
// zasobu nie ma (plik jeszcze nie dostarczony/dolaczony do buildu), Load()
// zwraca null i wywolujacy cicho wraca do Segoe UI.
public static class EmbeddedFontLoader
{
    private static readonly Dictionary<string, FontFamily?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static FontFamily? Load(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var cached)) return cached;

        FontFamily? family = null;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    var bytes = buffer.ToArray();

                    // PrivateFontCollection.AddMemoryFont wymaga niezarzadzanego
                    // bufora, ktory musi pozostac wazny przez caly czas zycia
                    // czcionki - to udokumentowane, zamierzone "przytrzymanie"
                    // pamieci (nie prawdziwy wyciek), akceptowalne dla appki
                    // dzialajacej caly czas w tle.
                    var handle = Marshal.AllocCoTaskMem(bytes.Length);
                    Marshal.Copy(bytes, 0, handle, bytes.Length);

                    var collection = new PrivateFontCollection();
                    collection.AddMemoryFont(handle, bytes.Length);

                    if (collection.Families.Length > 0)
                        family = collection.Families[0];
                }
            }
        }
        catch
        {
            family = null;
        }

        Cache[fileName] = family;
        return family;
    }
}
