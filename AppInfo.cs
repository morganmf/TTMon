namespace TTMon;

public static class AppInfo
{
    public const string AppName = "TTMon";
    public const string Tagline = "CPU/GPU/WAN Delay Tray Monitor";
    public const string Author = "Blaoyne";

    public const string Version = "0.13-beta";

    // Uwaga: to jest data ostatniej modyfikacji pliku .exe na dysku, nie
    // "prawdziwa" data kompilacji osadzona przez kompilator (.NET domyslnie
    // tego nie robi bez dodatkowej konfiguracji deterministic-build) -
    // w praktyce dla lokalnego dotnet build/publish to i tak to samo.
    //
    // Environment.ProcessPath (nie Assembly.Location!) - Assembly.Location
    // zwraca PUSTY string w trybie PublishSingleFile (ostrzezenie kompilatora
    // IL3000), bo zbudowany .exe to samorozpakowujacy sie kontener, nie
    // zwykle zestawienie .NET z normalna sciezka na dysku.
    public static DateTime BuildDate
    {
        get
        {
            try
            {
                var path = Environment.ProcessPath;
                return path != null ? File.GetLastWriteTime(path) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
