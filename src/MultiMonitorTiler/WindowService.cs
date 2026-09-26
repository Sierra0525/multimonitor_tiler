using System.Diagnostics;
using System.Text;

namespace MultiMonitorTiler;

internal sealed record WindowInfo(IntPtr Handle, string Title, uint ProcessId, string? ProcessName);

internal static class WindowService
{
    public static List<WindowInfo> GetVisibleWindows()
    {
        var result = new List<WindowInfo>();
        IntPtr shellWindow = Native.GetShellWindow();

        Native.EnumWindows((IntPtr hWnd, IntPtr _) =>
        {
            if (hWnd == shellWindow) return true;
            if (!Native.IsWindowVisible(hWnd)) return true;
            if (Native.GetAncestor(hWnd, Native.GA_ROOT) != hWnd) return true;

            int length = Native.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            Native.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            Native.GetWindowThreadProcessId(hWnd, out uint pid);
            string? processName = null;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
            }
            catch
            {
                // Process exited between enumeration and lookup, or access denied
                // (elevated process while this tool runs unelevated) — skip details.
            }

            result.Add(new WindowInfo(hWnd, title, pid, processName));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static WindowInfo? FindByTitleFragment(IEnumerable<WindowInfo> windows, string fragment)
    {
        return windows.FirstOrDefault(w =>
            w.Title.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
