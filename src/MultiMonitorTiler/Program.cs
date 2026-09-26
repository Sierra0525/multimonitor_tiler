using System.Windows.Forms;

namespace MultiMonitorTiler;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        return command switch
        {
            "list-monitors" => CommandListMonitors(),
            "list-windows" => CommandListWindows(),
            "mirror" => CommandMirror(options),
            _ => CommandUnknown(command),
        };
    }

    private static int CommandUnknown(string command)
    {
        Console.Error.WriteLine($"不明なコマンドです: {command}");
        PrintUsage();
        return 1;
    }

    private static Dictionary<string, string> ParseOptions(string[] rest)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rest.Length; i++)
        {
            if (!rest[i].StartsWith("--")) continue;
            string key = rest[i][2..];
            string value = "true";
            if (i + 1 < rest.Length && !rest[i + 1].StartsWith("--"))
            {
                value = rest[i + 1];
                i++;
            }
            dict[key] = value;
        }
        return dict;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            MultiMonitorTiler - 指定したウィンドウをキャプチャし、複数モニターに拡大ミラー表示します

            使い方:
              MultiMonitorTiler.exe list-monitors
              MultiMonitorTiler.exe list-windows
              MultiMonitorTiler.exe mirror --window "<タイトルの一部>" --monitors 1,2,3

            オプション:
              --monitors    対象モニター番号のカンマ区切り、または "all"

            mirror 実行中のホットキー(コントロールパネルウィンドウにフォーカスがある状態):
              Tab           調整対象モニターを切替
              矢印キー      パン (Shiftで大きく)
              + / -         ズーム (Shiftで大きく)
              R             現在のモニターの補正をリセット
              S             補正値を今すぐ保存
              Esc           終了(自動保存)

              補正値はモニターの組み合わせごとに保存され、次回以降の mirror 実行時に自動適用されます。
            """);
    }

    private static int CommandListMonitors()
    {
        var monitors = MonitorService.GetMonitors();
        Console.WriteLine("番号  位置(左,上)      解像度        Windowsスケール  プライマリ");
        foreach (var m in monitors)
        {
            Console.WriteLine(
                $"{m.Index,2}    ({m.Bounds.Left,5},{m.Bounds.Top,5})   {m.Bounds.Width,4}x{m.Bounds.Height,-4}   {m.ScalePercent,3}%             {(m.IsPrimary ? "*" : "")}");
        }
        Console.WriteLine();
        Console.WriteLine("※ここでの「Windowsスケール」は100%/125%などのOS設定であり、モニター実物の画素密度(実DPI)の違いとは別です。");
        Console.WriteLine("  実DPIの違いによる境界ズレは mirror コマンド実行中のホットキーで手動補正してください。");
        return 0;
    }

    private static int CommandListWindows()
    {
        var windows = WindowService.GetVisibleWindows();
        Console.WriteLine("PID     プロセス名            タイトル");
        foreach (var w in windows)
        {
            Console.WriteLine($"{w.ProcessId,-7} {(w.ProcessName ?? "?"),-20}  {w.Title}");
        }
        return 0;
    }

    private static int CommandMirror(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("window", out string? titleFragment))
        {
            Console.Error.WriteLine("エラー: --window \"<タイトルの一部>\" を指定してください。");
            return 1;
        }

        var windows = WindowService.GetVisibleWindows();
        var target = WindowService.FindByTitleFragment(windows, titleFragment);
        if (target is null)
        {
            Console.Error.WriteLine($"エラー: タイトルに \"{titleFragment}\" を含むウィンドウが見つかりません。");
            return 1;
        }

        if (Native.IsIconic(target.Handle))
        {
            Console.WriteLine("対象ウィンドウが最小化されているため復元します(最小化中はミラー表示が更新されません)。");
            Native.ShowWindow(target.Handle, ShowWindowCommand.SW_RESTORE);
        }

        var allMonitors = MonitorService.GetMonitors();
        var selectedMonitors = SelectMonitors(allMonitors, options.GetValueOrDefault("monitors"));
        if (selectedMonitors is null) return 1;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var controller = new MirrorController(target.Handle, selectedMonitors);
        try
        {
            controller.Start();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"「{target.Title}」をモニター {string.Join(",", selectedMonitors.Select(m => m.Index))} にミラー表示中です。");
        Console.WriteLine("コントロールパネルのウィンドウでホットキー操作してください。閉じると終了します。");

        using var panel = new ControlPanelForm(controller);
        Application.Run(panel);
        return 0;
    }

    private static List<MonitorInfo>? SelectMonitors(List<MonitorInfo> allMonitors, string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return allMonitors;
        }

        var indices = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int n) ? n : -1)
            .ToList();

        if (indices.Any(i => i <= 0))
        {
            Console.Error.WriteLine("エラー: --monitors には番号をカンマ区切りで指定してください(例: 1,2)。list-monitors で番号を確認してください。");
            return null;
        }

        var selected = allMonitors.Where(m => indices.Contains(m.Index)).ToList();
        if (selected.Count != indices.Distinct().Count())
        {
            Console.Error.WriteLine("エラー: 指定したモニター番号の一部が見つかりません。list-monitors で確認してください。");
            return null;
        }
        return selected;
    }
}
