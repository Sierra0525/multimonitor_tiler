using System.Drawing;
using System.Windows.Forms;

namespace MultiMonitorTiler;

/// <summary>
/// A borderless, topmost, click-through-free black canvas covering exactly one
/// monitor. The DWM thumbnail is drawn by the compositor on top of this window;
/// this form itself never paints the mirrored content.
/// </summary>
internal sealed class MonitorHostForm : Form
{
    public MonitorInfo Monitor { get; }

    public MonitorHostForm(MonitorInfo monitor)
    {
        Monitor = monitor;

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = false;

        Bounds = new Rectangle(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
