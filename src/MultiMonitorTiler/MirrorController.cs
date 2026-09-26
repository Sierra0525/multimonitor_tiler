namespace MultiMonitorTiler;

internal sealed class MonitorMirror
{
    public required MonitorInfo Monitor { get; init; }
    public required MonitorHostForm Host { get; init; }
    public required MonitorCalibration Calibration { get; set; }
    public IntPtr ThumbnailId { get; set; }
}

/// <summary>
/// Owns one DWM thumbnail per selected monitor, all sourced from the same target
/// window, and re-applies each one's source crop rectangle whenever its calibration
/// changes. DWM itself keeps the thumbnails' pixel content live — this class only
/// ever touches the (cheap) source/destination rectangles, never pixel data, which is
/// what keeps hotkey adjustments feeling instant.
/// </summary>
internal sealed class MirrorController : IDisposable
{
    private readonly IntPtr _sourceHwnd;
    private readonly RECT _unionBounds;
    private readonly string _profileKey;

    public List<MonitorMirror> Mirrors { get; }
    public int ActiveIndex { get; private set; }

    public MirrorController(IntPtr sourceHwnd, List<MonitorInfo> monitors)
    {
        _sourceHwnd = sourceHwnd;
        _unionBounds = MonitorService.UnionBounds(monitors);
        _profileKey = CalibrationStore.BuildProfileKey(monitors);

        var saved = CalibrationStore.Load(_profileKey);

        Mirrors = monitors.Select(m => new MonitorMirror
        {
            Monitor = m,
            Host = new MonitorHostForm(m),
            Calibration = saved.TryGetValue(m.DeviceName, out var cal) ? cal.Clone() : new MonitorCalibration(),
        }).ToList();
    }

    public MonitorMirror Active => Mirrors[ActiveIndex];

    public void Start()
    {
        foreach (var mirror in Mirrors)
        {
            mirror.Host.Show();
            int hr = Native.DwmRegisterThumbnail(mirror.Host.Handle, _sourceHwnd, out IntPtr thumbId);
            if (hr != 0)
            {
                throw new InvalidOperationException(
                    $"モニター {mirror.Monitor.Index} 用のサムネイル登録に失敗しました (HRESULT=0x{hr:X8})。対象ウィンドウが最小化されていないか確認してください。");
            }
            mirror.ThumbnailId = thumbId;
            Apply(mirror);
        }
    }

    public void SwitchActive(int direction)
    {
        ActiveIndex = ((ActiveIndex + direction) % Mirrors.Count + Mirrors.Count) % Mirrors.Count;
    }

    public void AdjustActiveZoom(double deltaPercent)
    {
        Active.Calibration.AdjustZoom(deltaPercent);
        Apply(Active);
    }

    public void AdjustActiveOffset(double deltaXPercent, double deltaYPercent)
    {
        Active.Calibration.AdjustOffset(deltaXPercent, deltaYPercent);
        Apply(Active);
    }

    public void ResetActive()
    {
        Active.Calibration.Reset();
        Apply(Active);
    }

    public void SaveCalibration()
    {
        var byDevice = Mirrors.ToDictionary(m => m.Monitor.DeviceName, m => m.Calibration);
        CalibrationStore.Save(_profileKey, byDevice);
    }

    private void Apply(MonitorMirror mirror)
    {
        if (Native.DwmQueryThumbnailSourceSize(mirror.ThumbnailId, out SIZE sourceSize) != 0)
        {
            return;
        }

        RECT sourceRect = SourceRectCalculator.Compute(mirror.Monitor, _unionBounds, sourceSize, mirror.Calibration);
        var destRect = new RECT { Left = 0, Top = 0, Right = mirror.Host.ClientSize.Width, Bottom = mirror.Host.ClientSize.Height };

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = (uint)(DwmThumbnailFlags.RectDestination | DwmThumbnailFlags.RectSource
                              | DwmThumbnailFlags.Opacity | DwmThumbnailFlags.Visible),
            rcDestination = destRect,
            rcSource = sourceRect,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false,
        };

        Native.DwmUpdateThumbnailProperties(mirror.ThumbnailId, ref props);
    }

    public void Dispose()
    {
        foreach (var mirror in Mirrors)
        {
            if (mirror.ThumbnailId != IntPtr.Zero)
            {
                Native.DwmUnregisterThumbnail(mirror.ThumbnailId);
            }
            mirror.Host.Close();
            mirror.Host.Dispose();
        }
    }
}
