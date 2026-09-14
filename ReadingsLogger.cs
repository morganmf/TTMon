namespace TTMon;

// Dopisuje jedna linie na kazdy odczyt do pliku .txt (format latwy do otwarcia
// w Excelu - srednik jako separator). Wylaczone domyslnie, wlaczane w
// ustawieniach. Blad zapisu (np. brak uprawnien) NIE wywala aplikacji - po
// prostu dany wpis przepada.
public static class ReadingsLogger
{
    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TTMon", "readings.txt");

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
