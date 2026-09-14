using System.Runtime.InteropServices;

namespace TTMon;

// Calkowita ilosc zainstalowanego RAM przez natywne WinAPI (GlobalMemoryStatusEx) -
// nie wymaga uprawnien administratora, w przeciwienstwie do sensorow LibreHardwareMonitorLib.
public static class SystemInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static double? GetTotalRamGb()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
                return status.ullTotalPhys / 1024d / 1024d / 1024d;
        }
        catch
        {
            // Brak - RAM po prostu nie zostanie pokazany w oknie Info
        }
        return null;
    }
}
