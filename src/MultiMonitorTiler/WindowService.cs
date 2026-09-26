using System.Diagnostics;
using System.Text;

namespace MultiMonitorTiler;

internal sealed record WindowInfo(IntPtr Handle, string Title, uint ProcessId, string? ProcessName, string? ExecutablePath);

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
            string? exePath = null;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
                exePath = process.MainModule?.FileName;
            }
            catch
            {
                // Process exited between enumeration and lookup, or access denied
                // (elevated process while this tool runs unelevated) — skip details.
            }

            result.Add(new WindowInfo(hWnd, title, pid, processName, exePath));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static WindowInfo? FindByTitleFragment(IEnumerable<WindowInfo> windows, string fragment)
    {
        return windows.FirstOrDefault(w =>
            w.Title.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static RECT GetVisibleFrameBounds(IntPtr hWnd)
    {
        if (Native.DwmGetWindowAttribute(hWnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwmRect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) == 0)
        {
            return dwmRect;
        }
        Native.GetWindowRect(hWnd, out RECT rect);
        return rect;
    }

    /// <summary>
    /// Moves/resizes the target window so its VISIBLE frame (DWM extended frame bounds,
    /// i.e. the part the user actually sees — excluding the invisible resize-grip margin
    /// that Win32 still counts as part of the window rect) lines up exactly with
    /// <paramref name="targetVisibleBounds"/>. Without this compensation the window would
    /// be offset by a few pixels relative to the monitor edges.
    /// </summary>
    public static void ExpandToBounds(IntPtr hWnd, RECT targetVisibleBounds)
    {
        if (Native.IsZoomed(hWnd) || Native.IsIconic(hWnd))
        {
            Native.ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);
        }

        Native.GetWindowRect(hWnd, out RECT rawRect);
        RECT visibleRect = GetVisibleFrameBounds(hWnd);

        int leftPad = visibleRect.Left - rawRect.Left;
        int topPad = visibleRect.Top - rawRect.Top;
        int rightPad = rawRect.Right - visibleRect.Right;
        int bottomPad = rawRect.Bottom - visibleRect.Bottom;

        int x = targetVisibleBounds.Left - leftPad;
        int y = targetVisibleBounds.Top - topPad;
        int width = targetVisibleBounds.Width + leftPad + rightPad;
        int height = targetVisibleBounds.Height + topPad + bottomPad;

        Native.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height,
            SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_FRAMECHANGED);
    }
}
