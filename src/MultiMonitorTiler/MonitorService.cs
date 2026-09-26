namespace MultiMonitorTiler;

internal sealed record MonitorInfo(
    int Index,
    IntPtr Handle,
    string DeviceName,
    RECT Bounds,
    uint DpiX,
    uint DpiY,
    bool IsPrimary)
{
    public double ScalePercent => Math.Round(DpiX / 96.0 * 100.0);
}

internal static class MonitorService
{
    public static List<MonitorInfo> GetMonitors()
    {
        var raw = new List<(IntPtr handle, RECT bounds, string device, bool primary)>();

        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT rect, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (Native.GetMonitorInfo(hMonitor, ref mi))
            {
                bool primary = (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0;
                raw.Add((hMonitor, mi.rcMonitor, mi.szDevice, primary));
            }
            return true;
        }, IntPtr.Zero);

        // Left-to-right, top-to-bottom ordering matches how Windows' own
        // Display Settings numbers monitors closely enough for users to recognize.
        var ordered = raw
            .OrderBy(m => m.bounds.Top)
            .ThenBy(m => m.bounds.Left)
            .ToList();

        var result = new List<MonitorInfo>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var (handle, bounds, device, primary) = ordered[i];
            Native.GetDpiForMonitor(handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
            result.Add(new MonitorInfo(i + 1, handle, device, bounds, dpiX, dpiY, primary));
        }
        return result;
    }

    public static RECT UnionBounds(IEnumerable<MonitorInfo> monitors)
    {
        var list = monitors.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one monitor must be selected.");

        var union = list[0].Bounds;
        foreach (var m in list.Skip(1))
        {
            union.Left = Math.Min(union.Left, m.Bounds.Left);
            union.Top = Math.Min(union.Top, m.Bounds.Top);
            union.Right = Math.Max(union.Right, m.Bounds.Right);
            union.Bottom = Math.Max(union.Bottom, m.Bounds.Bottom);
        }
        return union;
    }
}
