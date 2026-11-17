#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class SimulationProcessResources
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCounters
    {
        public uint Size, PageFaultCount;
        public UIntPtr PeakWorkingSet, WorkingSet, QuotaPeakPagedPool, QuotaPagedPool;
        public UIntPtr QuotaPeakNonPagedPool, QuotaNonPagedPool, PagefileUsage, PeakPagefileUsage;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);

    public static long PeakWorkingSet(Process process)
    {
        long measured = Math.Max(process.PeakWorkingSet64, process.WorkingSet64);
        if (measured > 0 || Environment.OSVersion.Platform != PlatformID.Win32NT) return measured;
        // Some Unity/Mono Process implementations return zero. Query only our owned child handle.
        var counters = new MemoryCounters { Size = (uint)Marshal.SizeOf(typeof(MemoryCounters)) };
        return GetProcessMemoryInfo(process.Handle, ref counters, counters.Size)
            ? checked((long)counters.PeakWorkingSet.ToUInt64()) : 0;
    }
}
#endif
