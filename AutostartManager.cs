using System.Diagnostics;

namespace TTMon;

// Autostart przez Harmonogram Zadan (schtasks.exe), NIE przez zwykly klucz
// rejestru Run - ten drugi przy programie z manifestem requireAdministrator
// dawalby prompt UAC przy KAZDYM logowaniu. Zadanie z /RL HIGHEST utworzone
// raz (z uprawnieniami administratora, ktore appka i tak juz ma) uruchamia
// sie podniesione bez ponownego pytania przy starcie systemu.
public static class AutostartManager
{
    private const string TaskName = "TTMon_Autostart";

    public static bool IsEnabled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // Zwraca false jesli operacja sie nie udala (np. schtasks.exe niedostepny) -
    // wolajacy powinien wtedy poinformowac usera, ze autostart nie zostal ustawiony.
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            var exePath = Application.ExecutablePath;
            string args = enabled
                ? $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F"
                : $"/Delete /TN \"{TaskName}\" /F";

            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process?.WaitForExit();

            // /Delete na nieistniejacym zadaniu tez zwraca kod != 0 - to nie jest
            // realny blad z punktu widzenia usera (efekt koncowy jest ten sam:
            // zadania nie ma), wiec przy wylaczaniu traktujemy to jako sukces.
            return process?.ExitCode == 0 || !enabled;
        }
        catch
        {
            return false;
        }
    }
}
