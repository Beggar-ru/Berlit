using System;
using System.Drawing;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// TabControl в тёмной теме: вкладки фиксированной ширины, выбранная —
    /// на фоне страницы с бирюзовой полосой сверху, под курсором — чуть светлее.
    /// Multiline = false, поэтому при нехватке места вкладки прокручиваются.
    /// </summary>
    public class DarkTabControl : TabControl
    {
        private int _hot = -1;
        private bool _compact;

        public DarkTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Multiline = false;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(132, 32);
            Padding = new Point(16, 6);
            Font = Fonts.Base;

            // Страницы рисуются поверх тела контрола, поэтому фон тела = фон окна.
            BackColor = Theme.Back;
        }

        /// <summary>Компактные вкладки для вложенных панелей (CPU/GPU/RAM/...).</summary>
        public bool Compact
        {
            get { return _compact; }
            set
            {
                _compact = value;
                if (!value) return;

                ItemSize = new Size(112, 28);
                Padding = new Point(12, 4);
                Font = Fonts.Small;

                if (IsHandleCreated)
                {
                    RecreateHandle();
                }
            }
        }

        private int TabAt(Point point)
        {
            for (int i = 0; i < TabCount; i++)
                if (GetTabRect(i).Contains(point)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = TabAt(e.Location);
            if (hot != _hot)
            {
                _hot = hot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != -1)
            {
                _hot = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);

            for (int i = 0; i < TabCount; i++)
            {
                Rectangle bounds = GetTabRect(i);
                bool selected = i == SelectedIndex;

                Color back = selected ? Theme.Surface : (i == _hot ? Theme.SurfaceAlt : Theme.Back);
                using (var brush = new SolidBrush(back))
                    g.FillRectangle(brush, bounds);

                if (selected)
                {
                    using (var brush = new SolidBrush(Theme.Accent))
                        g.FillRectangle(brush, bounds.X, bounds.Y, bounds.Width, 2);
                }

                TextRenderer.DrawText(g, TabPages[i].Text, selected ? Fonts.BaseBold : Font, bounds,
                    selected ? Theme.Text : Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }
}
