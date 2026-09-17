namespace TTMon;

// Dopisuje jedna linie na kazdy odczyt do pliku .csv (srednik jako separator -
// Excel otwiera to natywnie jako czytelna tabele przy dwukliku, mozna od razu
// robic wykresy tak samo jak z prawdziwego .xlsx). Wylaczone domyslnie,
// wlaczane w ustawieniach. Blad zapisu (np. brak uprawnien) NIE wywala
// aplikacji - po prostu dany wpis przepada.
//
// UWAGA: swiadomie NIE .xlsx - ten format trzeba wczytac do pamieci w calosci,
// dopisac wiersz i zapisac caly plik na nowo (to ZIP z XML w srodku, nie da
// sie po prostu dopisac linijki na koncu). Przy domyslnym interwale 2s to
// dziesiatki tysiecy wierszy dziennie - odczyt+zapis calego pliku przy KAZDEJ
// probce zacząłby zauwazalnie spowalniac appke, tym bardziej im dluzej dziala.
// .csv skaluje sie bez tego problemu, a Excel i tak otwiera go jako tabele.
public static class ReadingsLogger
{
    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TTMon", "readings.csv");

    public static void Log(DateTime timestamp, float? cpuTempC, float? gpuTempC, float? vrmTempC, int? wanLatencyMs)
    {
        try
        {
            var path = LogFilePath;
            var isNewFile = !File.Exists(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, append: true);
            if (isNewFile)
                writer.WriteLine("Data;CPU (C);GPU (C);VRM MOS (C);WAN (ms)");

            var cpuText = cpuTempC?.ToString("0.0") ?? "";
            var gpuText = gpuTempC?.ToString("0.0") ?? "";
            var vrmText = vrmTempC?.ToString("0.0") ?? "";
            var wanText = wanLatencyMs?.ToString() ?? "";

            writer.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss};{cpuText};{gpuText};{vrmText};{wanText}");
        }
        catch
        {
            // Cichy blad - logowanie nie moze wywalic reszty aplikacji
            // (np. brak uprawnien do zapisu, dysk pelny)
        }
    }
}
