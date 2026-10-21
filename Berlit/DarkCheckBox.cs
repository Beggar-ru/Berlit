using System;
using System.Drawing;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// Флажок в тёмной теме. Системный CheckBox рисуется светлым квадратом,
    /// который выбивается из интерфейса, поэтому квадрат и галочка рисуются здесь.
    /// </summary>
    public sealed class DarkCheckBox : CheckBox
    {
        private const int BoxSize = 14;

        private bool _hover;

        public DarkCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.SurfaceAlt;
            ForeColor = Theme.TextSensor;
            Font = Fonts.Small;
            AutoSize = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var back = new SolidBrush(BackColor))
                g.FillRectangle(back, ClientRectangle);

            int top = (Height - BoxSize) / 2;
            var box = new Rectangle(0, top, BoxSize, BoxSize);

            Color border = Focused ? Theme.Accent : (_hover ? Theme.TextDim : Theme.Border);
            using (var pen = new Pen(border))
                g.DrawRectangle(pen, box);

            if (Checked)
            {
                using (var pen = new Pen(Theme.Accent, 2f))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLines(pen, new[]
                    {
                        new Point(box.X + 3, box.Y + 7),
                        new Point(box.X + 6, box.Y + 10),
                        new Point(box.X + 11, box.Y + 4)
                    });
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                }
            }

            int textX = BoxSize + 7;
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(textX, 0, Math.Max(0, Width - textX), Height),
                Enabled ? ForeColor : Theme.TextFaint,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }
}
