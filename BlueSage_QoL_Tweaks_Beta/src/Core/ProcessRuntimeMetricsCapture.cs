using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlueSage.QoLTweaks.Core;

internal static class ProcessRuntimeMetricsCapture
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx
    {
        internal uint Size;
        internal uint PageFaultCount;
        internal UIntPtr PeakWorkingSetSize;
        internal UIntPtr WorkingSetSize;
        internal UIntPtr QuotaPeakPagedPoolUsage;
        internal UIntPtr QuotaPagedPoolUsage;
        internal UIntPtr QuotaPeakNonPagedPoolUsage;
        internal UIntPtr QuotaNonPagedPoolUsage;
        internal UIntPtr PagefileUsage;
        internal UIntPtr PeakPagefileUsage;
        internal UIntPtr PrivateUsage;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr process,
        out ProcessMemoryCountersEx counters,
        uint size);

    public static long? ReadWorkingSetBytes()
    {
        try
        {
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                TryReadWindowsProcessMemory(process, out long nativeWorkingSetBytes);
                return SelectPositiveBytes(process.WorkingSet64, nativeWorkingSetBytes);
            }
        }
        catch
        {
            return null;
        }
    }

    internal static long? SelectPositiveBytes(long primary, long fallback)
    {
        if (primary > 0L)
        {
            return primary;
        }

        return fallback > 0L ? fallback : (long?)null;
    }

    private static bool TryReadWindowsProcessMemory(
        Process process,
        out long workingSetBytes)
    {
        workingSetBytes = 0L;
        if (process == null || Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            return false;
        }

        try
        {
            var counters = new ProcessMemoryCountersEx
            {
                Size = (uint)Marshal.SizeOf(typeof(ProcessMemoryCountersEx)),
            };
            if (!GetProcessMemoryInfo(process.Handle, out counters, counters.Size))
            {
                return false;
            }

            workingSetBytes = ToPositiveInt64(counters.WorkingSetSize);
            return workingSetBytes > 0L;
        }
        catch
        {
            return false;
        }
    }

    private static long ToPositiveInt64(UIntPtr value)
    {
        ulong raw = value.ToUInt64();
        return raw == 0UL
            ? 0L
            : raw > long.MaxValue
                ? long.MaxValue
                : (long)raw;
    }
}
