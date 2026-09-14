namespace TTMon;

// Ekran powitalny pokazywany przy starcie appki: fade in -> trzymanie -> fade out,
// razem dokladnie 3 sekundy, potem okno samo sie zamyka. Bezobsluzone (bez X,
// bez paska tytulu) - user nie musi nic klikac.
public sealed class SplashForm : Form
{
    private enum Phase { FadeIn, Hold, FadeOut }

    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(2400);
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(900);
    // FadeIn + Hold + FadeOut = 4000ms (wczesniej 3000ms - wydluzone o 1s na zyczenie)

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private Phase _phase = Phase.FadeIn;
    private DateTime _phaseStart = DateTime.UtcNow;

    public SplashForm(Image image)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0d;

        var pictureBox = new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
        };
        Controls.Add(pictureBox);

        // Rozmiar okna proporcjonalny do obrazka, ograniczony DWOMA limitami:
        // % ekranu roboczego (zeby bylo bezpiecznie na malych ekranach) ORAZ
        // twardy limit w pikselach (zeby nie bylo za duze na duzych monitorach,
        // gdzie 18% i tak daje sporo pikseli) - brany mniejszy z obu.
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        var maxWidth = Math.Min((int)(workArea.Width * 0.18), 360);
        var maxHeight = Math.Min((int)(workArea.Height * 0.18), 220);
        var scale = Math.Min((float)maxWidth / image.Width, (float)maxHeight / image.Height);
        scale = Math.Min(scale, 1f); // nie powiekszamy ponad oryginalny rozmiar
        ClientSize = new Size((int)(image.Width * scale), (int)(image.Height * scale));

        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.UtcNow - _phaseStart;

        switch (_phase)
        {
            case Phase.FadeIn:
                Opacity = Math.Min(1d, elapsed.TotalMilliseconds / FadeInDuration.TotalMilliseconds);
                if (elapsed >= FadeInDuration)
                {
                    _phase = Phase.Hold;
                    _phaseStart = DateTime.UtcNow;
                }
                break;

            case Phase.Hold:
                Opacity = 1d;
                if (elapsed >= HoldDuration)
                {
                    _phase = Phase.FadeOut;
                    _phaseStart = DateTime.UtcNow;
                }
                break;

            case Phase.FadeOut:
                Opacity = Math.Max(0d, 1d - elapsed.TotalMilliseconds / FadeOutDuration.TotalMilliseconds);
                if (elapsed >= FadeOutDuration)
                {
                    _timer.Stop();
                    Close();
                }
                break;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }
}
