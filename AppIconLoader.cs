namespace TTMon;

// Wyciaga ikone z WLASNEGO uruchomionego .exe (ta sama co ApplicationIcon w
// .csproj) i uzywa jej jako ikony okna (lewy gorny rog paska tytulowego,
// Alt+Tab) - bez tego kazde nasze okno miaoby domyslna, generyczna ikone
// WinForms zamiast wlasnej marki. Wczytane raz, buforowane.
public static class AppIconLoader
{
    private static Icon? _cached;
    private static bool _attempted;

    public static Icon? Load()
    {
        if (_attempted) return _cached;
        _attempted = true;

        try
        {
            _cached = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // Brak - okno po prostu zostanie z domyslna ikona WinForms,
            // bez wywalania appki
            _cached = null;
        }

        return _cached;
    }
}
