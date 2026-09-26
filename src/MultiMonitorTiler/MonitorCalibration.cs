namespace MultiMonitorTiler;

/// <summary>
/// Per-monitor manual correction for real pixel-density (physical DPI) differences
/// between monitors — distinct from Windows' 100%/125%/150% scaling setting. Tuned
/// live via hotkeys so the mirrored image lines up across monitor bezels.
/// </summary>
internal sealed class MonitorCalibration
{
    public double ZoomPercent { get; set; } = 100.0;
    public double OffsetXPercent { get; set; } = 0.0;
    public double OffsetYPercent { get; set; } = 0.0;

    public const double MinZoom = 20.0;
    public const double MaxZoom = 400.0;
    public const double MaxOffset = 150.0;

    public void AdjustZoom(double deltaPercent) =>
        ZoomPercent = Math.Clamp(ZoomPercent + deltaPercent, MinZoom, MaxZoom);

    public void AdjustOffset(double deltaXPercent, double deltaYPercent)
    {
        OffsetXPercent = Math.Clamp(OffsetXPercent + deltaXPercent, -MaxOffset, MaxOffset);
        OffsetYPercent = Math.Clamp(OffsetYPercent + deltaYPercent, -MaxOffset, MaxOffset);
    }

    public void Reset()
    {
        ZoomPercent = 100.0;
        OffsetXPercent = 0.0;
        OffsetYPercent = 0.0;
    }

    public MonitorCalibration Clone() => new()
    {
        ZoomPercent = ZoomPercent,
        OffsetXPercent = OffsetXPercent,
        OffsetYPercent = OffsetYPercent,
    };
}
