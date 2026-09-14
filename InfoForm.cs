namespace TTMon;

public sealed class InfoForm : Form
{
    public InfoForm(string? cpuName, string? gpuName, double? ramGb, IReadOnlyList<(string Name, float? Value, string Unit)> motherboardSensors)
    {
        Text = Localization.T("info_title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;

        const int contentWidth = 320;
        int y = 15;

        var image = BrandingImage.Load();
        if (image != null)
        {
            var imgHeight = (int)(contentWidth * ((float)image.Height / image.Width));
            var pictureBox = new PictureBox
            {
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Left = 15,
                Top = y,
                Width = contentWidth,
                Height = imgHeight,
            };
            Controls.Add(pictureBox);
            y += imgHeight + 12;
        }

        // Naglowek marki: nazwa + tagline + autor/rok
        Controls.Add(new Label
        {
            Text = $"{AppInfo.AppName} v{AppInfo.Version}",
            Left = 15, Top = y, Width = contentWidth,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        });
        y += 28;

        Controls.Add(new Label
        {
            Text = AppInfo.Tagline,
            Left = 15, Top = y, Width = contentWidth,
            TextAlign = ContentAlignment.MiddleCenter,
        });
        y += 20;

        Controls.Add(new Label
        {
            Text = $"by {AppInfo.Author} 2026",
            Left = 15, Top = y, Width = contentWidth,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleCenter,
        });
        y += 26;

        // Rozdzielacz przed informacjami technicznymi
        Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Left = 15, Top = y, Width = contentWidth, Height = 2 });
        y += 12;

        var unknown = Localization.T("info_unknown");
        AddInfoLine($"{Localization.T("info_cpu")}: {cpuName ?? unknown}", ref y, contentWidth);
        AddInfoLine($"{Localization.T("info_gpu")}: {gpuName ?? unknown}", ref y, contentWidth);
        AddInfoLine($"{Localization.T("info_ram")}: {(ramGb is double gb ? $"{gb:0} GB" : unknown)}", ref y, contentWidth);

        y += 6;
        AddInfoLine($"{Localization.T("info_build_date")}: {AppInfo.BuildDate:yyyy-MM-dd HH:mm}", ref y, contentWidth);

        // Sekcja diagnostyczna "na probe" - wszystkie sensory plyty glownej,
        // zeby zobaczyc co faktycznie zglasza dany model przed zdecydowaniem
        // co pokazywac na stale gdzie indziej w appce.
        y += 10;
        Controls.Add(new Label
        {
            Text = Localization.T("info_motherboard_title"),
            Left = 15, Top = y, Width = contentWidth,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        });
        y += 20;

        var sensorText = motherboardSensors.Count > 0
            ? string.Join(Environment.NewLine, motherboardSensors.Select(s =>
                $"{s.Name}: {(s.Value is float v ? v.ToString("0.0") : "n/a")} {s.Unit}"))
            : Localization.T("info_motherboard_none");

        var sensorBox = new TextBox
        {
            Left = 15, Top = y, Width = contentWidth, Height = 150,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8f),
            Text = sensorText,
        };
        Controls.Add(sensorBox);
        y += 150 + 10;

        y += 5;
        var okBtn = new Button { Text = Localization.T("ok"), Left = (contentWidth - 80) / 2 + 15, Top = y, Width = 80, DialogResult = DialogResult.OK };
        Controls.Add(okBtn);
        AcceptButton = okBtn;
        CancelButton = okBtn;

        ClientSize = new Size(contentWidth + 30, y + 50);
    }

    private void AddInfoLine(string text, ref int y, int contentWidth)
    {
        Controls.Add(new Label { Text = text, Left = 15, Top = y, Width = contentWidth, AutoSize = false });
        y += 22;
    }
}
