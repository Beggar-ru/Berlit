using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>
    /// Панель «параметр — значение» для вкладки подробностей: секции с заголовками,
    /// перенос длинных значений, сворачивание секций, прокрутка и копирование.
    /// Рисуется вручную, чтобы совпадать по стилю с деревом датчиков.
    /// </summary>
    public sealed class InfoPanel : Control
    {
        private const int LabelWidthBase = 250;
        private const int HeaderHeightBase = 28;
        private const int RowHeightBase = 22;
        private const int LeftBase = 16;
        private const int ValueLeftBase = 280;
        private const int ValuePadBase = 16;
        private const int SectionGapBase = 8;

        private readonly List<InfoSection> _sections = new List<InfoSection>();
        private readonly DarkScrollBar _scroll = new DarkScrollBar();
        private readonly List<Hit> _hits = new List<Hit>();

        private InfoRow _selected;
        private Hit _hoverHit;
        private int _contentHeight;
        private Font _boldFont;
        private float _scale = 1f;

        private sealed class Hit
        {
            public InfoSection Section;
            public InfoRow Row;
            public int Top;
            public int Height;
            public bool IsHeader;
        }

        public InfoPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;

            _scroll.Dock = DockStyle.Right;
            _scroll.Visible = false;
            _scroll.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_scroll);

            UpdateFonts();
        }

        public InfoRow SelectedRow
        {
            get { return _selected; }
        }

        public void SetSections(IEnumerable<InfoSection> sections)
        {
            _sections.Clear();
            if (sections != null) _sections.AddRange(sections);

            _selected = null;
            _hoverHit = null;
            _scroll.Value = 0;
            Relayout();
            Invalidate();
        }

        public string GetPlainText()
        {
            var builder = new StringBuilder();
            foreach (InfoSection section in _sections)
            {
                builder.AppendLine("== " + section.Title + " ==");
                foreach (InfoRow row in section.Rows)
                    builder.AppendLine(row.Label + ": " + row.Value);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public void CopySelectedOrAll()
        {
            string text = _selected != null ? _selected.Label + ": " + _selected.Value : GetPlainText();
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
            Relayout();
            Invalidate();
        }

        private void UpdateFonts()
        {
            if (_boldFont != null) _boldFont.Dispose();
            _boldFont = new Font(Font, FontStyle.Bold);
            _scale = Font.Height / 15f;
            if (_scale <= 0) _scale = 1f;
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private int ContentWidth
        {
            get { return _scroll.Visible ? Width - _scroll.Width : Width; }
        }

        private int ValueWidth
        {
            get { return Math.Max(80, ContentWidth - S(ValueLeftBase) - S(ValuePadBase)); }
        }

        // ---------- layout ----------

        private void Relayout()
        {
            _hits.Clear();
            int y = 0;
            int valueWidth = ValueWidth;

            foreach (InfoSection section in _sections)
            {
                _hits.Add(new Hit { Section = section, IsHeader = true, Top = y, Height = S(HeaderHeightBase) });
                y += S(HeaderHeightBase);

                if (!section.Collapsed)
                {
                    foreach (InfoRow row in section.Rows)
                    {
                        int height = MeasureRow(row, valueWidth);
                        _hits.Add(new Hit { Section = section, Row = row, Top = y, Height = height });
                        y += height;
                    }
                }
                y += S(SectionGapBase);
            }

            _contentHeight = y;
            UpdateScroll();
        }

        private int MeasureRow(InfoRow row, int valueWidth)
        {
            int baseHeight = S(RowHeightBase);
            if (string.IsNullOrEmpty(row.Value)) return baseHeight;

            Size size = TextRenderer.MeasureText(row.Value, Font, new Size(valueWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return Math.Max(baseHeight, size.Height + S(6));
        }

        private void UpdateScroll()
        {
            int visible = Height;
            int maximum = Math.Max(0, _contentHeight - visible + 4);
            if (maximum > 0)
            {
                _scroll.Visible = true;
                _scroll.Minimum = 0;
                _scroll.Maximum = maximum;
                _scroll.LargeChange = Math.Max(1, visible);
                if (_scroll.Value > maximum) _scroll.Value = maximum;
            }
            else
            {
                _scroll.Value = 0;
                _scroll.Visible = false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
            Invalidate();
        }

        // ---------- painting ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            if (_sections.Count == 0)
            {
                TextRenderer.DrawText(g, "Reading hardware information...", Font,
                    new Rectangle(0, 0, ContentWidth, Height), Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            int offset = _scroll.Visible ? _scroll.Value : 0;
            int contentWidth = ContentWidth;

            foreach (Hit hit in _hits)
            {
                int top = hit.Top - offset;
                if (top + hit.Height < 0) continue;
                if (top > Height) break;

                if (hit.IsHeader) DrawHeader(g, hit, top, contentWidth);
                else DrawRow(g, hit, top, contentWidth);
            }

            using (var pen = new Pen(Theme.GridLine))
                g.DrawLine(pen, 0, Height - 1, contentWidth, Height - 1);
        }

        private void DrawHeader(Graphics g, Hit hit, int top, int width)
        {
            using (var brush = new SolidBrush(Theme.GroupBack))
                g.FillRectangle(brush, 0, top, width, hit.Height);

            using (var brush = new SolidBrush(Theme.Accent))
                g.FillRectangle(brush, 0, top, S(3), hit.Height);

            if (hit.Section.Collapsible)
            {
                int cx = S(14);
                int cy = top + hit.Height / 2;
                int size = S(4);
                Point[] points = hit.Section.Collapsed
                    ? new[] { new Point(cx - size, cy - size), new Point(cx - size, cy + size), new Point(cx + size, cy) }
                    : new[] { new Point(cx - size, cy - size), new Point(cx + size, cy - size), new Point(cx, cy + size) };

                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Theme.Accent))
                    g.FillPolygon(brush, points);
                g.SmoothingMode = old;
            }

            int textX = hit.Section.Collapsible ? S(24) : S(14);
            TextRenderer.DrawText(g, hit.Section.Title, _boldFont,
                new Rectangle(textX, top, Math.Max(0, width - textX - S(10)), hit.Height),
                Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private void DrawRow(Graphics g, Hit hit, int top, int width)
        {
            bool selected = hit.Row == _selected;
            bool hover = hit == _hoverHit && !selected;

            if (selected || hover)
            {
                using (var brush = new SolidBrush(selected ? Theme.Selection : Theme.Hover))
                    g.FillRectangle(brush, 0, top, width, hit.Height);
            }

            int labelWidth = S(LabelWidthBase);
            TextRenderer.DrawText(g, hit.Row.Label, Font,
                new Rectangle(S(LeftBase), top, labelWidth, hit.Height),
                Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                               TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            Color valueColor = hit.Row.Level != HwLevel.Normal ? Theme.Level(hit.Row.Level) : Theme.Text;
            TextRenderer.DrawText(g, hit.Row.Value, Font,
                new Rectangle(S(ValueLeftBase), top + S(2), ValueWidth, hit.Height - S(2)),
                valueColor, TextFormatFlags.Left | TextFormatFlags.Top |
                            TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }

        // ---------- mouse / keyboard ----------

        private Hit HitAt(int y)
        {
            int offset = _scroll.Visible ? _scroll.Value : 0;
            foreach (Hit hit in _hits)
            {
                if (y >= hit.Top - offset && y < hit.Top - offset + hit.Height) return hit;
            }
            return null;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_scroll.Visible) return;

            int step = S(64);
            _scroll.Value = Math.Max(_scroll.Minimum,
                Math.Min(_scroll.Maximum, _scroll.Value - (e.Delta / 120) * step));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Hit hit = HitAt(e.Y);
            if (hit != _hoverHit)
            {
                _hoverHit = hit;
                Cursor = hit != null && hit.IsHeader && hit.Section.Collapsible ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverHit != null)
            {
                _hoverHit = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Hit hit = HitAt(e.Y);
            if (hit == null) return;

            if (hit.IsHeader)
            {
                if (hit.Section.Collapsible && e.Button == MouseButtons.Left)
                {
                    hit.Section.Collapsed = !hit.Section.Collapsed;
                    Relayout();
                    Invalidate();
                }
                return;
            }

            _selected = hit.Row;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (_selected != null) CopySelectedOrAll();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedOrAll();
                e.Handled = true;
                return;
            }

            var rows = new List<InfoRow>();
            foreach (Hit hit in _hits)
                if (!hit.IsHeader) rows.Add(hit.Row);
            if (rows.Count == 0) return;

            int index = _selected == null ? -1 : rows.IndexOf(_selected);
            int page = Math.Max(1, Height / S(RowHeightBase) - 1);

            switch (e.KeyCode)
            {
                case Keys.Down: index = Math.Min(rows.Count - 1, index + 1); break;
                case Keys.Up: index = Math.Max(0, index - 1); break;
                case Keys.PageDown: index = Math.Min(rows.Count - 1, index + page); break;
                case Keys.PageUp: index = Math.Max(0, index - page); break;
                case Keys.Home: index = 0; break;
                case Keys.End: index = rows.Count - 1; break;
                default: return;
            }

            e.Handled = true;
            _selected = rows[index];
            EnsureVisible(_selected);
            Invalidate();
        }

        private void EnsureVisible(InfoRow row)
        {
            if (!_scroll.Visible) return;

            foreach (Hit hit in _hits)
            {
                if (hit.Row != row) continue;

                int top = hit.Top;
                int bottom = top + hit.Height;
                if (top < _scroll.Value) _scroll.Value = top;
                else if (bottom > _scroll.Value + Height) _scroll.Value = bottom - Height;
                return;
            }
        }
    }
}
