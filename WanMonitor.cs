using System.Net.NetworkInformation;

namespace TTMon;

// Pomiar opoznienia WAN przez zwykly System.Net.NetworkInformation.Ping.
// UWAGA (wniosek z wczesniejszego projektu fhu-go): unikamy wlasnorecznego
// skladania pakietow ICMP - fabryczny Ping z .NET jest sprawdzony i stabilny.
public sealed class WanMonitor
{
    private readonly Ping _ping = new();

    public async Task<int?> MeasureLatencyMsAsync(string host, int timeoutMs = 1500)
    {
        try
        {
            var reply = await _ping.SendPingAsync(host, timeoutMs);
            if (reply.Status == IPStatus.Success)
                return (int)reply.RoundtripTime;
            return null; // timeout / brak odpowiedzi = traktujemy jako "offline"
        }
        catch
        {
            return null;
        }
    }
}
