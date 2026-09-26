using System.Text.Json;

namespace MultiMonitorTiler;

/// <summary>
/// Persists per-monitor-set calibration, keyed by the set of physical monitors being
/// mirrored to (not by the mirrored window) — the real pixel-density mismatch this
/// corrects for is a property of the monitors, so a tuned profile keeps applying
/// whichever window the user mirrors later.
/// </summary>
internal static class CalibrationStore
{
    private static string StoreDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiMonitorTiler");

    private static string StoreFile => Path.Combine(StoreDirectory, "mirror-calibration.json");

    public static string BuildProfileKey(IEnumerable<MonitorInfo> monitors) =>
        string.Join("|", monitors.Select(m => m.DeviceName).OrderBy(n => n, StringComparer.Ordinal));

    public static Dictionary<string, MonitorCalibration> Load(string profileKey)
    {
        var all = LoadAll();
        if (all.TryGetValue(profileKey, out var profile))
        {
            return profile;
        }
        return new Dictionary<string, MonitorCalibration>();
    }

    public static void Save(string profileKey, Dictionary<string, MonitorCalibration> calibrationByDeviceName)
    {
        var all = LoadAll();
        all[profileKey] = calibrationByDeviceName;
        Directory.CreateDirectory(StoreDirectory);
        File.WriteAllText(StoreFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Dictionary<string, Dictionary<string, MonitorCalibration>> LoadAll()
    {
        if (!File.Exists(StoreFile)) return new Dictionary<string, Dictionary<string, MonitorCalibration>>();
        try
        {
            var json = File.ReadAllText(StoreFile);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, MonitorCalibration>>>(json)
                   ?? new Dictionary<string, Dictionary<string, MonitorCalibration>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, MonitorCalibration>>();
        }
    }
}
