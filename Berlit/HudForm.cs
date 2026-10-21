using System;
using System.Drawing;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// РћРІРµСЂР»РµР№ РїРѕРІРµСЂС… РІСЃРµС… РѕРєРѕРЅ: РєРѕРјРїР°РєС‚РЅР°СЏ РіСЂР°С„РёС‚РѕРІР°СЏ РїР»Р°С€РєР°, РІ РєРѕС‚РѕСЂРѕР№ РєР°Р¶РґР°СЏ
    /// РјРµС‚СЂРёРєР° РёРґС‘С‚ СЃРІРѕРµР№ СЃС‚СЂРѕРєРѕР№ вЂ” FPS, CPU, GPU, RAM. РћРєРЅРѕ РЅРµ Р·Р°Р±РёСЂР°РµС‚ С„РѕРєСѓСЃ,
    /// РЅРµ РїРѕРєР°Р·С‹РІР°РµС‚СЃСЏ РІ РїР°РЅРµР»Рё Р·Р°РґР°С‡ Рё РїСЂРѕРїСѓСЃРєР°РµС‚ РєР»РёРєРё СЃРєРІРѕР·СЊ СЃРµР±СЏ: РµРіРѕ РЅРµР»СЊР·СЏ
    /// РЅРё РЅР°Р¶Р°С‚СЊ, РЅРё РїРµСЂРµС‚Р°С‰РёС‚СЊ, РЅРё СЃР»СѓС‡Р°Р№РЅРѕ Р·Р°РєСЂС‹С‚СЊ.
    /// </summary>
    internal sealed class HudForm : Form
    {
        // Р Р°Р·РјРµСЂС‹ Рё РєРѕРѕСЂРґРёРЅР°С‚С‹ Р·Р°РґР°РЅС‹ РІ РµРґРёРЅРёС†Р°С… 96 DPI Рё СѓРјРЅРѕР¶Р°СЋС‚СЃСЏ РЅР° _scale.
        private const float BaseWidth = 236f;
        private const float BaseHeight = 98f;

        private const float LeftMargin = 12f;
        private const float LabelWidth = 34f;
        private const float LoadLeft = 58f;
        private const float LoadWidth = 62f;
        private const float TempLeft = 122f;
        private const float TempWidth = 58f;

        private const float RowBrand = 6f;
        private const float RowCpu = 28f;
        private const float RowGpu = 50f;
        private const float RowRam = 72f;

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
            Opacity = 0.9f;
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
                cp.ExStyle |= 0x00000020;   // WS_EX_TRANSPARENT вЂ” РєР»РёРєРё РїСЂРѕС…РѕРґСЏС‚ СЃРєРІРѕР·СЊ РѕРєРЅРѕ
                cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE вЂ” РЅРµ Р·Р°Р±РёСЂР°РµС‚ С„РѕРєСѓСЃ
                cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW вЂ” РЅРµ РІ Alt+Tab
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

            // РЎС‚СЂРѕРєР° 1: РёРјСЏ РїСЂРѕРіСЂР°РјРјС‹ Рё FPS.
            DrawText(g, "Berlit", _brandFont, Theme.TextDim, LeftMargin, RowBrand, 100, 16, HorizontalAlignment.Left);
            DrawText(g, "FPS", _labelFont, Theme.TextFaint, 142, RowBrand + 1, 30, 15, HorizontalAlignment.Right);
            DrawText(g, FormatFps(), _valueFont,
                _fps.Available ? Theme.Accent : Theme.TextFaint,
                176, RowBrand - 1, 54, 18, HorizontalAlignment.Right);

            // РЎС‚СЂРѕРєРё 2-4: РїРѕ РѕРґРЅРѕР№ РјРµС‚СЂРёРєРµ РЅР° СЃС‚СЂРѕРєСѓ.
            DrawMetric(g, RowCpu, "CPU", FormatLoad(_snapshot.CpuLoad), _snapshot.CpuTemp);
            DrawMetric(g, RowGpu, "GPU",
                _snapshot.HasGpu ? FormatLoad(_snapshot.GpuLoad) : "\u2014",
                _snapshot.HasGpu ? _snapshot.GpuTemp : null);

            DrawText(g, "RAM", _labelFont, Theme.TextFaint, LeftMargin, RowRam, LabelWidth, 16, HorizontalAlignment.Left);
            DrawText(g, FormatPercent(_snapshot.RamPercent), _valueFont, Theme.Accent,
                LoadLeft, RowRam - 1, LoadWidth, 18, HorizontalAlignment.Right);
        }

        /// <summary>РЎС‚СЂРѕРєР° РјРµС‚СЂРёРєРё: РїРѕРґРїРёСЃСЊ, Р·Р°РіСЂСѓР·РєР° Рё С‚РµРјРїРµСЂР°С‚СѓСЂР° РІ СЃРІРѕРёС… РєРѕР»РѕРЅРєР°С….</summary>
        private void DrawMetric(Graphics g, float y, string label, string load, double? temperature)
        {
            DrawText(g, label, _labelFont, Theme.TextFaint, LeftMargin, y, LabelWidth, 16, HorizontalAlignment.Left);
            DrawText(g, load, _valueFont, Theme.Accent, LoadLeft, y - 1, LoadWidth, 18, HorizontalAlignment.Right);

            double? temp = temperature;
            string text = FormatTemp(temp);
            Color color = !temp.HasValue ? Theme.TextFaint : TemperatureColor(temp.Value);
            DrawText(g, text, _valueFont, color, TempLeft, y - 1, TempWidth, 18, HorizontalAlignment.Right);
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
