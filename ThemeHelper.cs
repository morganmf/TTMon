using System.Runtime.InteropServices;

namespace TTMon;

// Recznie koloruje okna na ciemno (WinForms nie ma wbudowanego dark mode) i
// przelacza ciemny pasek tytulowy przez DWM (Windows 10 1809+/11). Wolane z
// Form.Load (nie z konstruktora) - DwmSetWindowAttribute wymaga juz
// utworzonego uchwytu okna (Handle), ktory Load gwarantuje.
public static class ThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // starsze buildy Windows 10 (1809-1903)

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkControlBackground = Color.FromArgb(45, 45, 45);
    private static readonly Color DarkForeground = Color.FromArgb(230, 230, 230);
    private static readonly Color DarkBorder = Color.FromArgb(80, 80, 80);

    public static void Apply(Form form, bool dark)
    {
        try
        {
            int value = dark ? 1 : 0;
            var result = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            if (result != 0)
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
        }
        catch
        {
            // Starszy Windows bez wsparcia dla tego atrybutu DWM - zostaje
            // jasny pasek tytulowy, nic sie nie wywala
        }

        if (!dark) return; // jasny motyw = domyslne kolory WinForms, nic do zmiany

        form.BackColor = DarkBackground;
        form.ForeColor = DarkForeground;
        ApplyToControls(form.Controls);
    }

    private static void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            switch (control)
            {
                case GroupBox gb:
                    gb.ForeColor = DarkForeground;
                    break;
                case Label lbl:
                    lbl.ForeColor = DarkForeground;
                    break;
                case Button btn:
                    btn.BackColor = DarkControlBackground;
                    btn.ForeColor = DarkForeground;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = DarkBorder;
                    break;
                case ComboBox cmb:
                    cmb.BackColor = DarkControlBackground;
                    cmb.ForeColor = DarkForeground;
                    break;
                case NumericUpDown num:
                    num.BackColor = DarkControlBackground;
                    num.ForeColor = DarkForeground;
                    break;
                case TextBox txt:
                    txt.BackColor = DarkControlBackground;
                    txt.ForeColor = DarkForeground;
                    break;
                case CheckBox chk:
                    chk.ForeColor = DarkForeground;
                    break;
                case ListView lv:
                    lv.BackColor = DarkControlBackground;
                    lv.ForeColor = DarkForeground;
                    break;
                case SparklineChart chart:
                    chart.BackColor = DarkControlBackground;
                    break;
            }

            if (control.HasChildren)
                ApplyToControls(control.Controls);
        }
    }
}
