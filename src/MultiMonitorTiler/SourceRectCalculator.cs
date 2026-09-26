namespace MultiMonitorTiler;

internal static class SourceRectCalculator
{
    /// <summary>
    /// Maps one monitor's physical position within the union of all selected monitors
    /// onto a proportional crop of the source window's content, then applies that
    /// monitor's manual zoom/pan calibration on top.
    /// </summary>
    public static RECT Compute(MonitorInfo monitor, RECT unionBounds, SIZE sourceSize, MonitorCalibration calibration)
    {
        double sliceLeft = (monitor.Bounds.Left - unionBounds.Left) / (double)unionBounds.Width * sourceSize.cx;
        double sliceTop = (monitor.Bounds.Top - unionBounds.Top) / (double)unionBounds.Height * sourceSize.cy;
        double sliceWidth = monitor.Bounds.Width / (double)unionBounds.Width * sourceSize.cx;
        double sliceHeight = monitor.Bounds.Height / (double)unionBounds.Height * sourceSize.cy;

        double zoom = calibration.ZoomPercent / 100.0;
        double cropWidth = sliceWidth / zoom;
        double cropHeight = sliceHeight / zoom;

        double centerX = sliceLeft + sliceWidth / 2.0 + calibration.OffsetXPercent / 100.0 * sliceWidth;
        double centerY = sliceTop + sliceHeight / 2.0 + calibration.OffsetYPercent / 100.0 * sliceHeight;

        double left = centerX - cropWidth / 2.0;
        double top = centerY - cropHeight / 2.0;
        double right = left + cropWidth;
        double bottom = top + cropHeight;

        left = Math.Clamp(left, 0, sourceSize.cx);
        top = Math.Clamp(top, 0, sourceSize.cy);
        right = Math.Clamp(right, 0, sourceSize.cx);
        bottom = Math.Clamp(bottom, 0, sourceSize.cy);

        if (right <= left) right = Math.Min(left + 1, sourceSize.cx);
        if (bottom <= top) bottom = Math.Min(top + 1, sourceSize.cy);

        return new RECT
        {
            Left = (int)Math.Round(left),
            Top = (int)Math.Round(top),
            Right = (int)Math.Round(right),
            Bottom = (int)Math.Round(bottom),
        };
    }
}
