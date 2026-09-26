using System.Text.Json;

namespace MultiMonitorTiler;

internal sealed record SavedWindowState(string ExecutablePath, string Title, int Left, int Top, int Width, int Height, bool WasMaximized);

/// <summary>
/// Remembers a window's pre-expand position/size, keyed by executable path, so
/// `restore` can put it back even after the process has been relaunched (new HWND).
/// </summary>
internal static class WindowStateStore
{
    private static string StoreDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiMonitorTiler");

    private static string StoreFile => Path.Combine(StoreDirectory, "window-states.json");

    public static void Save(SavedWindowState state)
    {
        var all = LoadAll();
        all[state.ExecutablePath] = state;
        Directory.CreateDirectory(StoreDirectory);
        File.WriteAllText(StoreFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static SavedWindowState? Find(string executablePath)
    {
        return LoadAll().GetValueOrDefault(executablePath);
    }

    public static void Remove(string executablePath)
    {
        var all = LoadAll();
        if (all.Remove(executablePath))
        {
            File.WriteAllText(StoreFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static Dictionary<string, SavedWindowState> LoadAll()
    {
        if (!File.Exists(StoreFile)) return new Dictionary<string, SavedWindowState>();
        try
        {
            var json = File.ReadAllText(StoreFile);
            return JsonSerializer.Deserialize<Dictionary<string, SavedWindowState>>(json)
                   ?? new Dictionary<string, SavedWindowState>();
        }
        catch
        {
            return new Dictionary<string, SavedWindowState>();
        }
    }
}
