namespace TTMon;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Program bez glownego okna - dziala wylacznie jako ikona w trayu.
        Application.Run(new TrayApplicationContext());
    }
}
