using Microsoft.Win32;

namespace MultiMonitorTiler;

/// <summary>
/// Applies/removes the same per-application DPI compatibility override exposed in
/// Explorer's Properties → Compatibility → "Override high DPI scaling behavior".
/// This is an application-level shim (HKCU AppCompatFlags\Layers), not a monitor or
/// system display setting — no monitor scaling percentage is touched.
///
/// Forcing an app to DPI-unaware makes Windows apply one uniform bitmap scale to its
/// entire window regardless of which monitor(s) it spans, so the content's magnification
/// no longer jumps at a monitor boundary between differently-scaled displays. The
/// trade-off: the app's own UI renders at 96 DPI and is then stretched by the OS, so text
/// may look softer than the app's native per-monitor rendering.
/// </summary>
internal static class DpiCompat
{
    private const string LayersKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string DpiUnawareFlag = "~ DPIUNAWARE";
    private const string GdiScaledDpiUnawareFlag = "~ GDIDPISCALING DPIUNAWARE";

    public static bool IsDpiUnawareOverrideSet(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: false);
        var existing = key?.GetValue(exePath) as string;
        return existing != null && existing.Contains("DPIUNAWARE", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetDpiUnawareOverride(string exePath, bool preferGdiScaling)
    {
        using var key = Registry.CurrentUser.CreateSubKey(LayersKeyPath, writable: true);
        string existing = key.GetValue(exePath) as string ?? string.Empty;

        var tokens = existing.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("DPIUNAWARE", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("HIGHDPIAWARE", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("GDIDPISCALING", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("~", StringComparison.Ordinal))
            .ToList();

        string flag = preferGdiScaling ? GdiScaledDpiUnawareFlag : DpiUnawareFlag;
        tokens.InsertRange(0, flag.Split(' '));

        key.SetValue(exePath, string.Join(' ', tokens), RegistryValueKind.String);
    }

    public static void ClearDpiOverride(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: true);
        if (key?.GetValue(exePath) is not string existing) return;

        var tokens = existing.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("DPIUNAWARE", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("GDIDPISCALING", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (tokens.Count == 0 || tokens.All(t => t == "~"))
        {
            key.DeleteValue(exePath, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(exePath, string.Join(' ', tokens), RegistryValueKind.String);
        }
    }
}
