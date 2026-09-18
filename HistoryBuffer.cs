namespace TTMon;

public sealed record HistorySample(
    DateTime Timestamp,
    float? CpuTempC,
    float? GpuTempC,
    float? VrmTempC,
    float? CpuFanRpm,
    int? WanLatencyMs,
    IReadOnlyDictionary<string, float> MotherboardSensors);

// Trzyma probki z ostatnich 180 sekund (staly czas, nie stala liczba probek -
// dziala poprawnie niezaleznie od RefreshIntervalMs). Dziala wylacznie na watku
// UI (dodawane w Timer.Tick, czytane w Paint dialogu) - bez blokad, bo WinForms
// jest jednowatkowe i tak.
public sealed class HistoryBuffer
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(180);

    private readonly List<HistorySample> _samples = new();

    public void Add(HistorySample sample)
    {
        _samples.Add(sample);
        var cutoff = DateTime.UtcNow - Window;
        _samples.RemoveAll(s => s.Timestamp < cutoff);
    }

    public IReadOnlyList<HistorySample> Snapshot() => _samples;
}
