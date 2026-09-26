using System.Drawing;
using System.Windows.Forms;

namespace MultiMonitorTiler;

/// <summary>
/// Small always-on-top control window that owns keyboard focus for live calibration.
/// The mirrored monitors themselves are borderless/non-activating, so all hotkeys are
/// handled here and applied to whichever monitor is currently "active".
/// </summary>
internal sealed class ControlPanelForm : Form
{
    private const double FineStep = 0.5;
    private const double CoarseStep = 2.0;

    private readonly MirrorController _controller;
    private readonly Label _status;

    public ControlPanelForm(MirrorController controller)
    {
        _controller = controller;

        Text = "MultiMonitorTiler";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        KeyPreview = true;
        ClientSize = new Size(360, 220);
        MaximizeBox = false;
        MinimizeBox = false;

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(10),
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };
        Controls.Add(_status);

        FormClosing += (_, _) => _controller.SaveCalibration();
        UpdateStatus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool coarse = keyData.HasFlag(Keys.Shift);
        double step = coarse ? CoarseStep : FineStep;

        switch (keyData & ~Keys.Shift)
        {
            case Keys.Tab:
                _controller.SwitchActive(+1);
                break;
            case Keys.Left:
                _controller.AdjustActiveOffset(-step, 0);
                break;
            case Keys.Right:
                _controller.AdjustActiveOffset(+step, 0);
                break;
            case Keys.Up:
                _controller.AdjustActiveOffset(0, -step);
                break;
            case Keys.Down:
                _controller.AdjustActiveOffset(0, +step);
                break;
            case Keys.Oemplus:
            case Keys.Add:
                _controller.AdjustActiveZoom(+step);
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                _controller.AdjustActiveZoom(-step);
                break;
            case Keys.R:
                _controller.ResetActive();
                break;
            case Keys.S:
                _controller.SaveCalibration();
                break;
            case Keys.Escape:
                Close();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }

        UpdateStatus();
        return true;
    }

    private void UpdateStatus()
    {
        var active = _controller.Active;
        _status.Text =
            $"アクティブ: モニター {active.Monitor.Index} ({_controller.ActiveIndex + 1}/{_controller.Mirrors.Count})\n" +
            $"ズーム    : {active.Calibration.ZoomPercent:0.0}%\n" +
            $"位置補正  : X {active.Calibration.OffsetXPercent:0.0}%  Y {active.Calibration.OffsetYPercent:0.0}%\n" +
            "\n" +
            "Tab       : 調整対象モニターを切替\n" +
            "矢印キー  : パン (Shiftで大きく)\n" +
            "+ / -     : ズーム (Shiftで大きく)\n" +
            "R         : このモニターをリセット\n" +
            "S         : 今すぐ保存\n" +
            "Esc       : 終了 (自動保存)";
    }
}
