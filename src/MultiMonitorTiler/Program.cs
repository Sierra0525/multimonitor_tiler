using MultiMonitorTiler;

Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();
var options = ParseOptions(args.Skip(1).ToArray());

switch (command)
{
    case "list-monitors":
        return CommandListMonitors();
    case "list-windows":
        return CommandListWindows();
    case "expand":
        return CommandExpand(options);
    case "restore":
        return CommandRestore(options);
    case "clear-dpi-fix":
        return CommandClearDpiFix(options);
    default:
        Console.Error.WriteLine($"不明なコマンドです: {command}");
        PrintUsage();
        return 1;
}

static Dictionary<string, string> ParseOptions(string[] rest)
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

static void PrintUsage()
{
    Console.WriteLine("""
        MultiMonitorTiler - 指定したウィンドウを複数モニターにまたがって拡大表示します

        使い方:
          MultiMonitorTiler.exe list-monitors
          MultiMonitorTiler.exe list-windows
          MultiMonitorTiler.exe expand --window "<タイトルの一部>" --monitors 1,2,3 [--fix-dpi] [--gdi-scaling] [--relaunch] [--yes]
          MultiMonitorTiler.exe restore --window "<タイトルの一部>"
          MultiMonitorTiler.exe clear-dpi-fix --window "<タイトルの一部>"

        オプション:
          --monitors    対象モニター番号のカンマ区切り、または "all"
          --fix-dpi     モニター間でDPIスケールが異なる場合、対象アプリに
                        「高DPIスケーリングの上書き = システム」相当の互換フラグを設定します
                        (モニター自体のDPI設定は変更しません)
          --gdi-scaling --fix-dpi と併用。GDI描画アプリ向けの鮮明化オプションを使います
          --relaunch    --fix-dpi と併用。フラグ反映のためアプリを自動的に再起動します
                        (未保存の作業内容は失われる可能性があります)
          --yes         確認プロンプトをスキップします
        """);
}

static int CommandListMonitors()
{
    var monitors = MonitorService.GetMonitors();
    Console.WriteLine("番号  位置(左,上)      解像度        DPIスケール  プライマリ");
    foreach (var m in monitors)
    {
        Console.WriteLine(
            $"{m.Index,2}    ({m.Bounds.Left,5},{m.Bounds.Top,5})   {m.Bounds.Width,4}x{m.Bounds.Height,-4}   {m.ScalePercent,3}%        {(m.IsPrimary ? "*" : "")}");
    }
    return 0;
}

static int CommandListWindows()
{
    var windows = WindowService.GetVisibleWindows();
    Console.WriteLine("PID     プロセス名            タイトル");
    foreach (var w in windows)
    {
        Console.WriteLine($"{w.ProcessId,-7} {(w.ProcessName ?? "?"),-20}  {w.Title}");
    }
    return 0;
}

static int CommandExpand(Dictionary<string, string> options)
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
    if (target.ExecutablePath is null)
    {
        Console.Error.WriteLine("エラー: 対象プロセスの実行ファイルパスを取得できませんでした(権限不足の可能性があります)。");
        return 1;
    }

    var allMonitors = MonitorService.GetMonitors();
    var selectedMonitors = SelectMonitors(allMonitors, options.GetValueOrDefault("monitors"));
    if (selectedMonitors is null) return 1;

    bool mixedDpi = MonitorService.HasMixedDpi(selectedMonitors);
    bool fixDpiRequested = options.ContainsKey("fix-dpi");
    bool gdiScaling = options.ContainsKey("gdi-scaling");
    bool relaunchRequested = options.ContainsKey("relaunch");
    bool skipConfirm = options.ContainsKey("yes");

    if (mixedDpi)
    {
        Console.WriteLine("警告: 選択したモニター間でDPIスケールが異なります。");
        Console.WriteLine("      このままではモニターの境界で表示倍率が不連続になり、境界がズレて見えます。");

        if (fixDpiRequested)
        {
            bool alreadySet = DpiCompat.IsDpiUnawareOverrideSet(target.ExecutablePath);
            if (!alreadySet)
            {
                DpiCompat.SetDpiUnawareOverride(target.ExecutablePath, gdiScaling);
                Console.WriteLine($"対象アプリ ({target.ExecutablePath}) に DPI 互換フラグを設定しました。");
                Console.WriteLine("この設定はアプリの「次回起動時」から有効になります(実行中のプロセスには反映されません)。");

                if (relaunchRequested)
                {
                    if (!skipConfirm && !Confirm("アプリを今すぐ再起動しますか?未保存の内容は失われる可能性があります"))
                    {
                        Console.WriteLine("再起動をキャンセルしました。手動でアプリを再起動してから再度 expand を実行してください。");
                        return 0;
                    }
                    bool relaunched = ProcessRelauncher.TryRelaunch(target.ProcessId, target.ExecutablePath, TimeSpan.FromSeconds(10));
                    if (!relaunched)
                    {
                        Console.Error.WriteLine("再起動に失敗しました。手動でアプリを再起動してから再度 expand を実行してください。");
                        return 1;
                    }
                    Console.WriteLine("再起動しました。反映を確認したうえで、再度 expand コマンドを実行してください。");
                    return 0;
                }
                else
                {
                    Console.WriteLine("手動でアプリを再起動したのち、再度 expand コマンドを実行してください。");
                    return 0;
                }
            }
            else
            {
                Console.WriteLine("DPI 互換フラグは既に設定済みです。境界のズレが残る場合はアプリを再起動済みか確認してください。");
            }
        }
        else if (!skipConfirm)
        {
            if (!Confirm("境界のズレを許容してこのまま拡大表示を続行しますか?(--fix-dpi の使用を推奨します)"))
            {
                Console.WriteLine("中止しました。--fix-dpi オプションの利用をご検討ください。");
                return 0;
            }
        }
    }

    var unionBounds = MonitorService.UnionBounds(selectedMonitors);

    Native.GetWindowRect(target.Handle, out RECT before);
    bool wasMaximized = Native.IsZoomed(target.Handle);
    WindowStateStore.Save(new SavedWindowState(
        target.ExecutablePath, target.Title, before.Left, before.Top, before.Width, before.Height, wasMaximized));

    WindowService.ExpandToBounds(target.Handle, unionBounds);

    Console.WriteLine($"「{target.Title}」をモニター {string.Join(",", selectedMonitors.Select(m => m.Index))} に拡大しました。");
    Console.WriteLine("元に戻すには: restore --window \"" + titleFragment + "\"");
    return 0;
}

static int CommandRestore(Dictionary<string, string> options)
{
    if (!options.TryGetValue("window", out string? titleFragment))
    {
        Console.Error.WriteLine("エラー: --window \"<タイトルの一部>\" を指定してください。");
        return 1;
    }

    var windows = WindowService.GetVisibleWindows();
    var target = WindowService.FindByTitleFragment(windows, titleFragment);
    if (target is null || target.ExecutablePath is null)
    {
        Console.Error.WriteLine($"エラー: タイトルに \"{titleFragment}\" を含むウィンドウが見つかりません。");
        return 1;
    }

    var saved = WindowStateStore.Find(target.ExecutablePath);
    if (saved is null)
    {
        Console.Error.WriteLine("このアプリの保存済みウィンドウ位置が見つかりません。");
        return 1;
    }

    var bounds = new RECT { Left = saved.Left, Top = saved.Top, Right = saved.Left + saved.Width, Bottom = saved.Top + saved.Height };
    WindowService.ExpandToBounds(target.Handle, bounds);
    if (saved.WasMaximized)
    {
        Native.ShowWindow(target.Handle, ShowWindowCommand.SW_RESTORE);
    }

    Console.WriteLine($"「{target.Title}」の位置を元に戻しました。");
    return 0;
}

static int CommandClearDpiFix(Dictionary<string, string> options)
{
    if (!options.TryGetValue("window", out string? titleFragment))
    {
        Console.Error.WriteLine("エラー: --window \"<タイトルの一部>\" を指定してください。");
        return 1;
    }

    var windows = WindowService.GetVisibleWindows();
    var target = WindowService.FindByTitleFragment(windows, titleFragment);
    if (target is null || target.ExecutablePath is null)
    {
        Console.Error.WriteLine($"エラー: タイトルに \"{titleFragment}\" を含むウィンドウが見つかりません。");
        return 1;
    }

    DpiCompat.ClearDpiOverride(target.ExecutablePath);
    Console.WriteLine($"「{target.ExecutablePath}」の DPI 互換フラグを解除しました。次回起動時から反映されます。");
    return 0;
}

static List<MonitorInfo>? SelectMonitors(List<MonitorInfo> allMonitors, string? spec)
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

static bool Confirm(string message)
{
    Console.Write($"{message} [y/N]: ");
    string? answer = Console.ReadLine();
    return answer?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
}
