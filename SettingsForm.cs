namespace TTMon;

// Dialog ustawien, uklad reczny (bez pliku Designer.cs), pogrupowany w sekcje
// GroupBox zeby nie zgubic sie w rosnacej liczbie opcji.
public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly ComboBox _languageBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _autostartBox = new();
    private readonly CheckBox _showSplashBox = new();
    private readonly CheckBox _enableLoggingBox = new();
    private readonly CheckBox _alwaysOnTopBox = new();
    private readonly CheckBox _darkModeBox = new();

    private readonly CheckBox _showCpuBox = new();
    private readonly CheckBox _showGpuBox = new();
    private readonly CheckBox _showVrmBox = new();
    private readonly CheckBox _showCpuFanBox = new();
    private readonly CheckBox _showWanBox = new();
    private readonly ComboBox _cpuVendorBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _gpuVendorBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _iconSizeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _trayFontBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _iconOutlineBox = new();
    private readonly CheckBox _iconBackgroundPlateBox = new();
    private readonly ComboBox _iconColorModeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly NumericUpDown _tempMinUpDown = new() { Minimum = -50, Maximum = 150, DecimalPlaces = 0 };
    private readonly NumericUpDown _tempMaxUpDown = new() { Minimum = -50, Maximum = 150, DecimalPlaces = 0 };

    private readonly NumericUpDown _wanMinUpDown = new() { Minimum = 0, Maximum = 5000, Increment = 10 };
    private readonly NumericUpDown _wanMaxUpDown = new() { Minimum = 0, Maximum = 5000, Increment = 10 };

    private readonly Button _saveBtn = new();

    // Stan autostartu przy otwarciu dialogu - porownywany przy zapisie, zeby
    // wolac AutostartManager tylko gdy user faktycznie cos zmienil
    private bool _autostartWasEnabled;

    // Dopoki trwa poczatkowe ladowanie wartosci (LoadFromSettings), zmiany
    // kontrolek NIE licza sie jako "user cos zmienil" - inaczej przycisk
    // Zapisz odblokowalby sie sam, zaraz po otwarciu okna.
    private bool _isLoading;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = Localization.T("settings_title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;

        BuildLayout();

        _isLoading = true;
        LoadFromSettings();
        _isLoading = false;

        Load += (_, _) => ThemeHelper.Apply(this, _settings.DarkMode);
    }

    private void BuildLayout()
    {
        const int formWidth = 360;
        int y = 15;

        y = AddGeneralGroup(formWidth, y);
        y = AddSensorsGroup(formWidth, y);
        y = AddTempGradientGroup(formWidth, y);
        y = AddWanGradientGroup(formWidth, y);

        // Podpinamy sledzenie zmian PO zbudowaniu wszystkich grup (zeby
        // zlapac kazda kontrolke), ale PRZED LoadFromSettings (ktore i tak
        // jest oslonione flaga _isLoading w konstruktorze).
        HookDirtyTracking(Controls);

        _saveBtn.Text = Localization.T("save");
        _saveBtn.Left = formWidth - 190; _saveBtn.Top = y; _saveBtn.Width = 80;
        _saveBtn.DialogResult = DialogResult.OK;
        _saveBtn.Enabled = false; // odblokowuje sie dopiero po pierwszej realnej zmianie
        _saveBtn.Click += (_, _) => { SaveToSettings(); Close(); };

        var cancelBtn = new Button { Text = Localization.T("close"), Left = formWidth - 100, Top = y, Width = 80, DialogResult = DialogResult.Cancel };
        cancelBtn.Click += (_, _) => Close();

        Controls.Add(_saveBtn);
        Controls.Add(cancelBtn);
        AcceptButton = _saveBtn;
        CancelButton = cancelBtn;

        ClientSize = new Size(formWidth, y + 50);
    }

    // Rekurencyjnie podpina wspolny handler MarkDirty pod kazda kontrolke
    // ustawien (checkbox/combo/numeric) - zeby nie trzeba bylo pamietac o
    // podpieciu recznie przy kazdej nowej opcji dodanej w przyszlosci.
    private void HookDirtyTracking(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            switch (c)
            {
                case CheckBox chk: chk.CheckedChanged += MarkDirty; break;
                case ComboBox cmb: cmb.SelectedIndexChanged += MarkDirty; break;
                case NumericUpDown num: num.ValueChanged += MarkDirty; break;
            }
            if (c.HasChildren) HookDirtyTracking(c.Controls);
        }
    }

    private void MarkDirty(object? sender, EventArgs e)
    {
        if (!_isLoading) _saveBtn.Enabled = true;
    }

    private int AddGeneralGroup(int formWidth, int top)
    {
        var group = new GroupBox { Text = Localization.T("section_general"), Left = 15, Top = top, Width = formWidth - 30, Height = 204 };
        int gy = 25;

        var langLabel = new Label { Text = Localization.T("language"), Left = 15, Top = gy, Width = 100 };
        _languageBox.Items.AddRange(new object[] { "Polski", "English" });
        _languageBox.Left = 150; _languageBox.Top = gy - 3; _languageBox.Width = 160;
        group.Controls.Add(langLabel);
        group.Controls.Add(_languageBox);
        gy += 34;

        _autostartBox.Text = Localization.T("autostart");
        _autostartBox.Left = 15; _autostartBox.Top = gy; _autostartBox.Width = 300;
        group.Controls.Add(_autostartBox);
        gy += 28;

        _showSplashBox.Text = Localization.T("show_splash");
        _showSplashBox.Left = 15; _showSplashBox.Top = gy; _showSplashBox.Width = 300;
        group.Controls.Add(_showSplashBox);
        gy += 28;

        _enableLoggingBox.Text = Localization.T("enable_logging");
        _enableLoggingBox.Left = 15; _enableLoggingBox.Top = gy; _enableLoggingBox.Width = 300;
        group.Controls.Add(_enableLoggingBox);
        gy += 28;

        _alwaysOnTopBox.Text = Localization.T("details_always_on_top");
        _alwaysOnTopBox.Left = 15; _alwaysOnTopBox.Top = gy; _alwaysOnTopBox.Width = 300;
        group.Controls.Add(_alwaysOnTopBox);
        gy += 28;

        _darkModeBox.Text = Localization.T("dark_mode");
        _darkModeBox.Left = 15; _darkModeBox.Top = gy; _darkModeBox.Width = 300;
        group.Controls.Add(_darkModeBox);

        Controls.Add(group);
        return top + group.Height + 10;
    }

    private int AddSensorsGroup(int formWidth, int top)
    {
        var group = new GroupBox { Text = Localization.T("section_sensors"), Left = 15, Top = top, Width = formWidth - 30, Height = 411 };
        int gy = 22;

        _showCpuBox.Text = Localization.T("show_cpu");
        _showCpuBox.Left = 15; _showCpuBox.Top = gy; _showCpuBox.Width = 300;
        group.Controls.Add(_showCpuBox);
        gy += 26;

        var cpuVendorLabel = new Label { Text = Localization.T("cpu_vendor"), Left = 15, Top = gy + 3, Width = 100 };
        _cpuVendorBox.Items.AddRange(new object[]
        {
            Localization.T("cpu_vendor_auto"), Localization.T("cpu_vendor_intel"), Localization.T("cpu_vendor_amd"),
        });
        _cpuVendorBox.Left = 150; _cpuVendorBox.Top = gy; _cpuVendorBox.Width = 160;
        group.Controls.Add(cpuVendorLabel);
        group.Controls.Add(_cpuVendorBox);
        gy += 32;

        _showGpuBox.Text = Localization.T("show_gpu");
        _showGpuBox.Left = 15; _showGpuBox.Top = gy; _showGpuBox.Width = 300;
        group.Controls.Add(_showGpuBox);
        gy += 26;

        var gpuVendorLabel = new Label { Text = Localization.T("gpu_vendor"), Left = 15, Top = gy + 3, Width = 100 };
        _gpuVendorBox.Items.AddRange(new object[]
        {
            Localization.T("gpu_vendor_auto"), Localization.T("gpu_vendor_intel"),
            Localization.T("gpu_vendor_amd"), Localization.T("gpu_vendor_nvidia"),
        });
        _gpuVendorBox.Left = 150; _gpuVendorBox.Top = gy; _gpuVendorBox.Width = 160;
        group.Controls.Add(gpuVendorLabel);
        group.Controls.Add(_gpuVendorBox);
        gy += 32;

        _showVrmBox.Text = Localization.T("show_vrm");
        _showVrmBox.Left = 15; _showVrmBox.Top = gy; _showVrmBox.Width = 300;
        group.Controls.Add(_showVrmBox);
        gy += 28;

        _showCpuFanBox.Text = Localization.T("show_cpu_fan");
        _showCpuFanBox.Left = 15; _showCpuFanBox.Top = gy; _showCpuFanBox.Width = 300;
        group.Controls.Add(_showCpuFanBox);
        gy += 28;

        _showWanBox.Text = Localization.T("show_wan");
        _showWanBox.Left = 15; _showWanBox.Top = gy; _showWanBox.Width = 300;
        group.Controls.Add(_showWanBox);
        gy += 32;

        var iconSizeLabel = new Label { Text = Localization.T("icon_size"), Left = 15, Top = gy + 3, Width = 130 };
        _iconSizeBox.Items.AddRange(new object[]
        {
            Localization.T("icon_size_small"), Localization.T("icon_size_medium"), Localization.T("icon_size_large"),
        });
        _iconSizeBox.Left = 150; _iconSizeBox.Top = gy; _iconSizeBox.Width = 160;
        group.Controls.Add(iconSizeLabel);
        group.Controls.Add(_iconSizeBox);
        gy += 32;

        var trayFontLabel = new Label { Text = Localization.T("tray_font"), Left = 15, Top = gy + 3, Width = 130 };
        _trayFontBox.Items.AddRange(new object[]
        {
            Localization.T("tray_font_segoe"), Localization.T("tray_font_bahnschrift"),
            Localization.T("tray_font_dosis"), Localization.T("tray_font_jetbrains"),
            Localization.T("tray_font_volvo"), Localization.T("tray_font_firacode_mono"),
            Localization.T("tray_font_firacode_propo"), Localization.T("tray_font_envycoder_mono"),
            Localization.T("tray_font_envycoder_propo"), Localization.T("tray_font_terminess_mono"),
            Localization.T("tray_font_terminess_propo"), Localization.T("tray_font_meslo"),
            Localization.T("tray_font_hurmit_mono"), Localization.T("tray_font_hurmit_propo"),
        });
        _trayFontBox.Left = 150; _trayFontBox.Top = gy; _trayFontBox.Width = 160;
        group.Controls.Add(trayFontLabel);
        group.Controls.Add(_trayFontBox);
        gy += 32;

        _iconOutlineBox.Text = Localization.T("icon_outline");
        _iconOutlineBox.Left = 15; _iconOutlineBox.Top = gy; _iconOutlineBox.Width = 300;
        group.Controls.Add(_iconOutlineBox);
        gy += 26;

        _iconBackgroundPlateBox.Text = Localization.T("icon_background_plate");
        _iconBackgroundPlateBox.Left = 15; _iconBackgroundPlateBox.Top = gy; _iconBackgroundPlateBox.Width = 300;
        group.Controls.Add(_iconBackgroundPlateBox);
        gy += 32;

        var iconColorModeLabel = new Label { Text = Localization.T("icon_color_mode"), Left = 15, Top = gy + 3, Width = 130 };
        _iconColorModeBox.Items.AddRange(new object[]
        {
            Localization.T("icon_color_mode_text"), Localization.T("icon_color_mode_bg"),
        });
        _iconColorModeBox.Left = 150; _iconColorModeBox.Top = gy; _iconColorModeBox.Width = 160;
        group.Controls.Add(iconColorModeLabel);
        group.Controls.Add(_iconColorModeBox);

        Controls.Add(group);
        return top + group.Height + 10;
    }

    private int AddTempGradientGroup(int formWidth, int top)
    {
        var group = new GroupBox { Text = Localization.T("section_gradient_temp"), Left = 15, Top = top, Width = formWidth - 30, Height = 75 };

        var minLabel = new Label { Text = Localization.T("gradient_temp_min"), Left = 15, Top = 30, Width = 90 };
        _tempMinUpDown.Left = 105; _tempMinUpDown.Top = 27; _tempMinUpDown.Width = 60;

        var maxLabel = new Label { Text = Localization.T("gradient_temp_max"), Left = 180, Top = 30, Width = 90 };
        _tempMaxUpDown.Left = 270; _tempMaxUpDown.Top = 27; _tempMaxUpDown.Width = 55;

        group.Controls.Add(minLabel);
        group.Controls.Add(_tempMinUpDown);
        group.Controls.Add(maxLabel);
        group.Controls.Add(_tempMaxUpDown);
        Controls.Add(group);

        return top + group.Height + 10;
    }

    private int AddWanGradientGroup(int formWidth, int top)
    {
        var group = new GroupBox { Text = Localization.T("section_gradient_wan"), Left = 15, Top = top, Width = formWidth - 30, Height = 75 };

        var minLabel = new Label { Text = Localization.T("gradient_wan_min"), Left = 15, Top = 30, Width = 90 };
        _wanMinUpDown.Left = 105; _wanMinUpDown.Top = 27; _wanMinUpDown.Width = 60;

        var maxLabel = new Label { Text = Localization.T("gradient_wan_max"), Left = 180, Top = 30, Width = 90 };
        _wanMaxUpDown.Left = 270; _wanMaxUpDown.Top = 27; _wanMaxUpDown.Width = 55;

        group.Controls.Add(minLabel);
        group.Controls.Add(_wanMinUpDown);
        group.Controls.Add(maxLabel);
        group.Controls.Add(_wanMaxUpDown);
        Controls.Add(group);

        return top + group.Height + 10;
    }

    private void LoadFromSettings()
    {
        _languageBox.SelectedIndex = _settings.Language == AppLanguage.PL ? 0 : 1;

        _autostartWasEnabled = AutostartManager.IsEnabled();
        _autostartBox.Checked = _autostartWasEnabled;
        _showSplashBox.Checked = _settings.ShowSplash;
        _enableLoggingBox.Checked = _settings.EnableLogging;
        _alwaysOnTopBox.Checked = _settings.DetailsAlwaysOnTop;
        _darkModeBox.Checked = _settings.DarkMode;

        _showCpuBox.Checked = _settings.ShowCpu;
        _showGpuBox.Checked = _settings.ShowGpu;
        _showVrmBox.Checked = _settings.ShowVrm;
        _showCpuFanBox.Checked = _settings.ShowCpuFan;
        _showWanBox.Checked = _settings.ShowWan;

        _cpuVendorBox.SelectedIndex = _settings.PreferredCpuVendor switch
        {
            CpuVendorPreference.Intel => 1,
            CpuVendorPreference.Amd => 2,
            _ => 0,
        };
        _gpuVendorBox.SelectedIndex = _settings.PreferredGpuVendor switch
        {
            GpuVendorPreference.Intel => 1,
            GpuVendorPreference.Amd => 2,
            GpuVendorPreference.Nvidia => 3,
            _ => 0,
        };
        _iconSizeBox.SelectedIndex = _settings.IconSize switch
        {
            IconSizeLevel.Small => 0,
            IconSizeLevel.Large => 2,
            _ => 1,
        };
        _trayFontBox.SelectedIndex = _settings.TrayFont switch
        {
            TrayFontChoice.Bahnschrift => 1,
            TrayFontChoice.Dosis => 2,
            TrayFontChoice.JetBrainsMono => 3,
            TrayFontChoice.VolvoBroad => 4,
            TrayFontChoice.FiraCodeMono => 5,
            TrayFontChoice.FiraCodePropo => 6,
            TrayFontChoice.EnvyCodeRMono => 7,
            TrayFontChoice.EnvyCodeRPropo => 8,
            TrayFontChoice.TerminessMono => 9,
            TrayFontChoice.TerminessPropo => 10,
            TrayFontChoice.MesloLGLMono => 11,
            TrayFontChoice.HurmitMono => 12,
            TrayFontChoice.HurmitPropo => 13,
            _ => 0,
        };
        _iconOutlineBox.Checked = _settings.IconOutline;
        _iconBackgroundPlateBox.Checked = _settings.IconBackgroundPlate;
        _iconColorModeBox.SelectedIndex = _settings.IconColorMode == IconColorMode.ColoredBackground ? 1 : 0;

        _tempMinUpDown.Value = (decimal)_settings.TempGradientMinC;
        _tempMaxUpDown.Value = (decimal)_settings.TempGradientMaxC;
        _wanMinUpDown.Value = _settings.WanGradientMinMs;
        _wanMaxUpDown.Value = _settings.WanGradientMaxMs;
    }

    private void SaveToSettings()
    {
        _settings.Language = _languageBox.SelectedIndex == 0 ? AppLanguage.PL : AppLanguage.EN;
        _settings.ShowSplash = _showSplashBox.Checked;
        _settings.EnableLogging = _enableLoggingBox.Checked;
        _settings.DetailsAlwaysOnTop = _alwaysOnTopBox.Checked;
        _settings.DarkMode = _darkModeBox.Checked;

        if (_autostartBox.Checked != _autostartWasEnabled)
        {
            var ok = AutostartManager.SetEnabled(_autostartBox.Checked);
            if (!ok)
                MessageBox.Show(this, Localization.T("autostart_failed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _settings.ShowCpu = _showCpuBox.Checked;
        _settings.ShowGpu = _showGpuBox.Checked;
        _settings.ShowVrm = _showVrmBox.Checked;
        _settings.ShowCpuFan = _showCpuFanBox.Checked;
        _settings.ShowWan = _showWanBox.Checked;

        _settings.PreferredCpuVendor = _cpuVendorBox.SelectedIndex switch
        {
            1 => CpuVendorPreference.Intel,
            2 => CpuVendorPreference.Amd,
            _ => CpuVendorPreference.Auto,
        };
        _settings.PreferredGpuVendor = _gpuVendorBox.SelectedIndex switch
        {
            1 => GpuVendorPreference.Intel,
            2 => GpuVendorPreference.Amd,
            3 => GpuVendorPreference.Nvidia,
            _ => GpuVendorPreference.Auto,
        };
        _settings.IconSize = _iconSizeBox.SelectedIndex switch
        {
            0 => IconSizeLevel.Small,
            2 => IconSizeLevel.Large,
            _ => IconSizeLevel.Medium,
        };
        _settings.TrayFont = _trayFontBox.SelectedIndex switch
        {
            1 => TrayFontChoice.Bahnschrift,
            2 => TrayFontChoice.Dosis,
            3 => TrayFontChoice.JetBrainsMono,
            4 => TrayFontChoice.VolvoBroad,
            5 => TrayFontChoice.FiraCodeMono,
            6 => TrayFontChoice.FiraCodePropo,
            7 => TrayFontChoice.EnvyCodeRMono,
            8 => TrayFontChoice.EnvyCodeRPropo,
            9 => TrayFontChoice.TerminessMono,
            10 => TrayFontChoice.TerminessPropo,
            11 => TrayFontChoice.MesloLGLMono,
            12 => TrayFontChoice.HurmitMono,
            13 => TrayFontChoice.HurmitPropo,
            _ => TrayFontChoice.SegoeUI,
        };
        _settings.IconOutline = _iconOutlineBox.Checked;
        _settings.IconBackgroundPlate = _iconBackgroundPlateBox.Checked;
        _settings.IconColorMode = _iconColorModeBox.SelectedIndex == 1 ? IconColorMode.ColoredBackground : IconColorMode.ColoredText;

        // Zabezpieczenie przed Max <= Min, ktore zepsuloby dzielenie w gradiencie
        var tempMin = (float)_tempMinUpDown.Value;
        var tempMax = (float)_tempMaxUpDown.Value;
        _settings.TempGradientMinC = tempMin;
        _settings.TempGradientMaxC = tempMax > tempMin ? tempMax : tempMin + 1;

        var wanMin = (int)_wanMinUpDown.Value;
        var wanMax = (int)_wanMaxUpDown.Value;
        _settings.WanGradientMinMs = wanMin;
        _settings.WanGradientMaxMs = wanMax > wanMin ? wanMax : wanMin + 1;

        _settings.Save();
    }
}
