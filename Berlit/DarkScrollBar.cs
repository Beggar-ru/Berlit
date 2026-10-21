using System;
using System.Drawing;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// Тонкая тёмная полоса прокрутки. Системная (VScrollBar) не подчиняется теме,
    /// поэтому рисуем свою: только трек и «ползунок», без стрелок.
    /// </summary>
    public sealed class DarkScrollBar : Control
    {
        private int _minimum;
        private int _maximum;
        private int _largeChange = 1;
        private int _value;
        private bool _hover;
        private bool _dragging;
        private int _dragOffset;

        public DarkScrollBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Width = 10;
            TabStop = false;
            Visible = false;
            BackColor = Theme.Surface;
        }

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = Math.Max(_minimum, value); }
        }

        public int LargeChange
        {
            get { return _largeChange; }
            set { _largeChange = Math.Max(1, value); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        private int ScrollableRange
        {
            get
            {
                int span = Math.Max(1, _maximum - _minimum + 1);
                return Math.Max(1, span - _largeChange);
            }
        }

        private Rectangle ThumbBounds
        {
            get
            {
                int trackTop = 2;
                int trackHeight = Height - 4;
                if (trackHeight <= 4) return Rectangle.Empty;

                int span = Math.Max(1, _maximum - _minimum + 1);
                int thumbHeight = (int)Math.Round((double)trackHeight * _largeChange / span);
                thumbHeight = Math.Max(26, Math.Min(trackHeight, thumbHeight));

                int free = trackHeight - thumbHeight;
                int offset = (int)Math.Round((double)free * (_value - _minimum) / ScrollableRange);
                return new Rectangle(2, trackTop + offset, Math.Max(3, Width - 4), thumbHeight);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var back = new SolidBrush(Theme.Surface))
                g.FillRectangle(back, ClientRectangle);

            Rectangle thumb = ThumbBounds;
            if (thumb.Height <= 0) return;

            Color color = _dragging ? Theme.AccentDark : (_hover ? Theme.ScrollHot : Theme.Scroll);
            using (var brush = new SolidBrush(color))
                g.FillRectangle(brush, thumb);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Rectangle thumb = ThumbBounds;
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.Y - thumb.Y;
                Capture = true;
            }
            else
            {
                // Клик по треку — страница вверх/вниз.
                Value = _value + (e.Y < thumb.Y ? -_largeChange : _largeChange);
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            Rectangle thumb = ThumbBounds;
            int trackTop = 2;
            int trackHeight = Math.Max(1, Height - 4);
            int free = Math.Max(1, trackHeight - thumb.Height);
            int top = Math.Max(0, Math.Min(free, e.Y - _dragOffset - trackTop));
            Value = _minimum + (int)Math.Round((double)top * ScrollableRange / free);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging) return;
            _dragging = false;
            Capture = false;
            Invalidate();
        }
    }
}
