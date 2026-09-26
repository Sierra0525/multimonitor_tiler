using System.Diagnostics;
using System.Management;

namespace MultiMonitorTiler;

internal static class ProcessRelauncher
{
    /// <summary>
    /// Best-effort restart of the process owning <paramref name="pid"/>: reads its
    /// original command line via WMI, asks it to close, waits briefly, then relaunches
    /// the same executable with the same arguments and working directory. Any in-app
    /// unsaved state is lost — callers must warn the user before invoking this.
    /// </summary>
    public static bool TryRelaunch(uint pid, string exePath, TimeSpan closeTimeout)
    {
        string arguments = GetCommandLineArguments(pid, exePath);
        string workingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;

        using var process = Process.GetProcessById((int)pid);
        process.CloseMainWindow();
        if (!process.WaitForExit((int)closeTimeout.TotalMilliseconds))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
        });
        return true;
    }

    private static string GetCommandLineArguments(uint pid, string exePath)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                string? commandLine = mo["CommandLine"] as string;
                if (string.IsNullOrEmpty(commandLine)) continue;

                // Strip the leading executable token, keep the rest as arguments.
                if (commandLine.StartsWith('"'))
                {
                    int closingQuote = commandLine.IndexOf('"', 1);
                    if (closingQuote > 0 && closingQuote + 1 < commandLine.Length)
                    {
                        return commandLine[(closingQuote + 1)..].TrimStart();
                    }
                    return string.Empty;
                }

                int firstSpace = commandLine.IndexOf(' ');
                return firstSpace > 0 ? commandLine[(firstSpace + 1)..] : string.Empty;
            }
        }
        catch
        {
            // WMI can fail without elevation for some processes — fall back to no args.
        }
        return string.Empty;
    }
}
