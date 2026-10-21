using System;
using System.Drawing;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// Оверлей поверх всех окон: маленькая графитовая плашка с бирюзовыми
    /// значениями CPU / GPU / RAM и FPS. Окно не забирает фокус, не показывается
    /// в панели задач и пропускает клики сквозь себя — его нельзя ни нажать,
    /// ни перетащить, ни случайно закрыть.
    /// </summary>
    internal sealed class HudForm : Form
    {
        private const float BaseWidth = 252f;
        private const float BaseHeight = 78f;

        private readonly FpsMeter _fps = new FpsMeter();
        private readonly Timer _timer = new Timer();
        private HudSnapshot _snapshot = new HudSnapshot();
        private float _scale = 1f;

        private Font _brandFont;
        private Font _labelFont;
        private Font _valueFont;

        public HudForm()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(10, 10, 10);
            Opacity = 1.0;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Berlit";
            Cursor = Cursors.Default;

            _timer.Interval = 1000;
            _timer.Tick += Timer_Tick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020;   // WS_EX_TRANSPARENT — клики проходят сквозь окно
                cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE — не забирает фокус
                cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW — не в Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyScale();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyScale();
        }

        private void ApplyScale()
        {
            _scale = DeviceDpi / 96f;

            DisposeFonts();
            _brandFont = new Font("Segoe UI", 7.5f * _scale, FontStyle.Bold);
            _labelFont = new Font("Segoe UI", 8.25f * _scale);
            _valueFont = new Font("Segoe UI", 9f * _scale, FontStyle.Bold);

            Size = new Size((int)(BaseWidth * _scale), (int)(BaseHeight * _scale));

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (int)(12 * _scale), area.Top + (int)(12 * _scale));

            Invalidate();
        }

        public void UpdateSnapshot(HudSnapshot snapshot)
        {
            if (snapshot == null) return;

            _snapshot = snapshot;
            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _fps.Start();
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            _fps.Dispose();
            DisposeFonts();
            base.OnFormClosed(e);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _fps.Tick();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            using (var accent = new SolidBrush(Theme.Accent))
                g.FillRectangle(accent, 0, 0, 3f * _scale, Height);

            using (var pen = new Pen(Color.FromArgb(46, 46, 46)))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            DrawText(g, "Berlit", _brandFont, Theme.TextDim, 12, 7, 80, 16, HorizontalAlignment.Left);

            DrawText(g, "FPS", _labelFont, Theme.TextFaint, BaseWidth - 98, 8, 30, 15, HorizontalAlignment.Right);
            DrawText(g, FormatFps(), _valueFont,
                _fps.Available ? Theme.Accent : Theme.TextFaint,
                BaseWidth - 64, 6, 50, 18, HorizontalAlignment.Right);

            DrawMetric(g, 12, 29, "CPU", FormatLoad(_snapshot.CpuLoad), _snapshot.CpuTemp);
            DrawMetric(g, 132, 29, "GPU",
                _snapshot.HasGpu ? FormatLoad(_snapshot.GpuLoad) : "\u2014",
                _snapshot.HasGpu ? _snapshot.GpuTemp : null);

            DrawText(g, "RAM", _labelFont, Theme.TextFaint, 12, 52, 34, 16, HorizontalAlignment.Left);
            DrawText(g, FormatPercent(_snapshot.RamPercent), _valueFont, Theme.Accent,
                48, 51, 60, 18, HorizontalAlignment.Left);
        }

        private void DrawMetric(Graphics g, float x, float y, string label, string load, double? temperature)
        {
            DrawText(g, label, _labelFont, Theme.TextFaint, x, y, 34, 16, HorizontalAlignment.Left);
            DrawText(g, load, _valueFont, Theme.Accent, x + 34, y - 1, 40, 18, HorizontalAlignment.Right);

            double? temp = temperature;
            string text = FormatTemp(temp);
            Color color = !temp.HasValue ? Theme.TextFaint : TemperatureColor(temp.Value);
            DrawText(g, text, _valueFont, color, x + 74, y - 1, 40, 18, HorizontalAlignment.Right);
        }

        private void DrawText(Graphics g, string text, Font font, Color color,
                              float x, float y, float width, float height, HorizontalAlignment align)
        {
            var rectangle = new Rectangle(
                (int)Math.Round(x * _scale),
                (int)Math.Round(y * _scale),
                (int)Math.Round(width * _scale),
                (int)Math.Round(height * _scale));

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            flags |= align == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;

            TextRenderer.DrawText(g, text ?? string.Empty, font, rectangle, color, flags);
        }

        private string FormatFps()
        {
            if (!_fps.Available) return "\u2014";

            double fps = _fps.Fps;
            if (fps < 0.5 || fps > 2000) return "\u2014";
            return ((int)Math.Round(fps)).ToString();
        }

        private static string FormatLoad(double value)
        {
            return ((int)Math.Round(value)).ToString() + "%";
        }

        private static string FormatLoad(double? value)
        {
            return value.HasValue ? FormatLoad(value.Value) : "\u2014";
        }

        private static string FormatPercent(double value)
        {
            return ((int)Math.Round(value)).ToString() + "%";
        }

        private static string FormatTemp(double? value)
        {
            return value.HasValue ? ((int)Math.Round(value.Value)).ToString() + "\u00B0" : "\u2014";
        }

        private static Color TemperatureColor(double celsius)
        {
            if (celsius >= 95) return Theme.Danger;
            if (celsius >= 80) return Theme.Warn;
            return Theme.Accent;
        }

        private void DisposeFonts()
        {
            if (_brandFont != null) { _brandFont.Dispose(); _brandFont = null; }
            if (_labelFont != null) { _labelFont.Dispose(); _labelFont = null; }
            if (_valueFont != null) { _valueFont.Dispose(); _valueFont = null; }
        }
    }
}
