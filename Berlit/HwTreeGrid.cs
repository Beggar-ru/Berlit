using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Berlit
{
    public sealed class HwColumnDef
    {
        public HwColumnDef(string header, int width, HorizontalAlignment align,
                           bool accent = false, bool bar = false)
        {
            Header = header;
            Width = width;
            Align = align;
            Accent = accent;
            Bar = bar;
        }

        public string Header { get; private set; }
        public int Width { get; private set; }
        public HorizontalAlignment Align { get; private set; }
        public bool Accent { get; private set; }   // значения рисуются бирюзовым
        public bool Bar { get; private set; }      // полоска заполнения за значением
    }

    /// <summary>
    /// Дерево с колонками в стиле HWMonitor: группы -> устройства -> датчики,
    /// параметры слева направо. Рисуется вручную, поэтому тысячи строк не проблема
    /// (отрисовываются только видимые), а полоса прокрутки и шапка полностью
    /// подчинены тёмной теме.
    /// </summary>
    public class HwTreeGrid : Control
    {
        private const int MinNameWidth = 200;
        private const int HeaderHeight = 27;
        private const int RowHeight = 24;
        private const int Indent = 22;

        private readonly List<HwNode> _roots = new List<HwNode>();
        private readonly List<HwNode> _rows = new List<HwNode>();
        private readonly List<HwColumnDef> _cols = new List<HwColumnDef>();
        private readonly DarkScrollBar _scroll = new DarkScrollBar();

        private string _filter = string.Empty;
        private HwNode _selected;
        private int _hoverRow = -1;
        private bool _hasData;
        private int _nameWidth;
        private int[] _widths = new int[0];

        public HwTreeGrid()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Fonts.Base;

            _scroll.Dock = DockStyle.Right;
            _scroll.Visible = false;
            _scroll.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_scroll);
        }

        [DefaultValue("Sensor")]
        public string NameHeader { get; set; } = "Sensor";

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string FilterText
        {
            get { return _filter; }
            set
            {
                _filter = (value ?? string.Empty).Trim();
                RebuildRows();
                _scroll.Value = 0;
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HwNode SelectedNode { get { return _selected; } }

        // ---------- public API ----------

        public void SetColumns(IEnumerable<HwColumnDef> columns)
        {
            _cols.Clear();
            _cols.AddRange(columns);
            _widths = new int[_cols.Count];
            Invalidate();
        }

        /// <summary>
        /// Заменяет дерево новыми данными. Состояние раскрытия и выделение
        /// сохраняются (по пути узла), поэтому подходит для периодического обновления.
        /// </summary>
        public void SetRoots(IEnumerable<HwNode> roots)
        {
            string selPath = _selected != null ? _selected.Path : null;
            var known = new Dictionary<string, bool>();
            CollectState(_roots, known);

            bool hadData = _hasData;
            _roots.Clear();
            _roots.AddRange(roots);
            if (hadData) RestoreState(_roots, known);
            _hasData = _roots.Count > 0;

            _selected = hadData && selPath != null ? FindByPath(_roots, selPath) : null;

            RebuildRows();
        }

        public void SetAllExpanded(bool expanded)
        {
            SetExpanded(_roots, expanded);
            RebuildRows();
        }

        // ---------- tree state ----------

        private static void CollectState(IEnumerable<HwNode> nodes, Dictionary<string, bool> map)
        {
            foreach (HwNode n in nodes)
            {
                map[n.Path] = n.Expanded;
                CollectState(n.Children, map);
            }
        }

        private static void RestoreState(IEnumerable<HwNode> nodes, Dictionary<string, bool> map)
        {
            foreach (HwNode n in nodes)
            {
                bool expanded;
                if (map.TryGetValue(n.Path, out expanded)) n.Expanded = expanded;
                RestoreState(n.Children, map);
            }
        }

        private static HwNode FindByPath(IEnumerable<HwNode> nodes, string path)
        {
            foreach (HwNode n in nodes)
            {
                if (n.Path == path) return n;
                HwNode found = FindByPath(n.Children, path);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetExpanded(IEnumerable<HwNode> nodes, bool expanded)
        {
            foreach (HwNode n in nodes)
            {
                n.Expanded = expanded;
                SetExpanded(n.Children, expanded);
            }
        }

        // ---------- rows / filter / scroll ----------

        /// <summary>Совпадение ищется и по имени строки, и по любому значению в колонках.</summary>
        private bool Matches(HwNode n)
        {
            if (_filter.Length == 0) return false;
            if (n.Text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            for (int i = 0; i < HwCol.Count; i++)
            {
                string value = n.GetValue(i);
                if (value.Length > 0 && value.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private bool AnyDescendantMatches(HwNode n)
        {
            foreach (HwNode c in n.Children)
            {
                if (Matches(c) || AnyDescendantMatches(c)) return true;
            }
            return false;
        }

        private void Flatten(HwNode n, bool parentMatched, List<HwNode> acc)
        {
            bool filtering = _filter.Length > 0;
            bool self = filtering && Matches(n);
            if (filtering && !parentMatched && !self && !AnyDescendantMatches(n)) return;

            acc.Add(n);
            if (!(filtering || n.Expanded)) return;

            bool passed = parentMatched || self;
            foreach (HwNode c in n.Children) Flatten(c, passed, acc);
        }

        private void RebuildRows()
        {
            _rows.Clear();
            foreach (HwNode root in _roots) Flatten(root, false, _rows);
            if (_hoverRow >= _rows.Count) _hoverRow = -1;
            UpdateScroll();
            Invalidate();
        }

        private int VisibleRows
        {
            get { return Math.Max(1, (Height - HeaderHeight) / RowHeight); }
        }

        private int ContentWidth
        {
            get { return _scroll.Visible ? Width - _scroll.Width : Width; }
        }

        private void UpdateScroll()
        {
            int visible = VisibleRows;
            if (_rows.Count > visible)
            {
                _scroll.Visible = true;
                _scroll.Minimum = 0;
                _scroll.Maximum = _rows.Count - 1;
                _scroll.LargeChange = visible;
                int maxTop = _rows.Count - visible;
                if (_scroll.Value > maxTop) _scroll.Value = maxTop;
            }
            else
            {
                _scroll.Value = 0;
                _scroll.Visible = false;
            }
        }

        private bool CanScroll
        {
            get { return _scroll.Visible && _scroll.Maximum > _scroll.LargeChange; }
        }

        private void EnsureVisible(int index)
        {
            if (!CanScroll) return;
            int top = _scroll.Value;
            int visible = VisibleRows;
            if (index < top) _scroll.Value = index;
            else if (index >= top + visible) _scroll.Value = index - visible + 1;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        // ---------- layout / metrics ----------

        private static int NameWidth(int contentWidth, IList<HwColumnDef> cols)
        {
            int fixedWidth = 0;
            for (int i = 0; i < cols.Count - 1; i++) fixedWidth += cols[i].Width;

            int name = (int)Math.Round(contentWidth * 0.30);
            name = Math.Max(MinNameWidth, Math.Min(380, name));

            int info = contentWidth - name - fixedWidth;
            if (info < 120) name = Math.Max(MinNameWidth, name - (120 - info));
            return name;
        }

        private void LayoutColumns(int contentWidth)
        {
            _nameWidth = NameWidth(contentWidth, _cols);
            if (_widths.Length != _cols.Count) _widths = new int[_cols.Count];

            int used = 0;
            for (int i = 0; i < _cols.Count - 1; i++)
            {
                _widths[i] = _cols[i].Width;
                used += _widths[i];
            }
            if (_cols.Count > 0)
                _widths[_cols.Count - 1] = Math.Max(60, contentWidth - _nameWidth - used);
        }

        private static TextFormatFlags Flags(HorizontalAlignment align)
        {
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            switch (align)
            {
                case HorizontalAlignment.Center: return flags | TextFormatFlags.HorizontalCenter;
                case HorizontalAlignment.Right: return flags | TextFormatFlags.Right;
                default: return flags | TextFormatFlags.Left;
            }
        }

        // ---------- painting ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            if (_cols.Count == 0) return;

            int contentWidth = ContentWidth;
            LayoutColumns(contentWidth);

            DrawHeader(g, contentWidth);

            int visible = VisibleRows;
            int first = _scroll.Visible ? _scroll.Value : 0;
            for (int i = 0; i < visible; i++)
            {
                int index = first + i;
                if (index >= _rows.Count) break;
                DrawRow(g, _rows[index], index, HeaderHeight + i * RowHeight, contentWidth);
            }

            DrawColumnLines(g);

            if (_rows.Count == 0)
            {
                string text = _filter.Length > 0
                                  ? "No matches for \u00AB" + _filter + "\u00BB"
                                  : (_hasData ? "No sensors" : "Reading hardware...");
                TextRenderer.DrawText(g, text, Font,
                    new Rectangle(0, HeaderHeight, contentWidth, Math.Max(0, Height - HeaderHeight)),
                    Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawHeader(Graphics g, int contentWidth)
        {
            using (var back = new SolidBrush(Theme.SurfaceAlt))
                g.FillRectangle(back, 0, 0, Width, HeaderHeight);

            TextRenderer.DrawText(g, NameHeader, Fonts.Header,
                new Rectangle(10, 0, Math.Max(0, _nameWidth - 12), HeaderHeight),
                Theme.TextDim, Flags(HorizontalAlignment.Left));

            int x = _nameWidth;
            for (int i = 0; i < _cols.Count; i++)
            {
                HwColumnDef column = _cols[i];
                int pad = column.Align == HorizontalAlignment.Left ? 8 : 0;
                TextRenderer.DrawText(g, column.Header, Fonts.Header,
                    new Rectangle(x + pad, 0, Math.Max(0, _widths[i] - pad), HeaderHeight), Theme.TextDim, Flags(column.Align));
                x += _widths[i];
            }

            using (var pen = new Pen(Theme.Accent, 2))
                g.DrawLine(pen, 0, HeaderHeight - 1, contentWidth, HeaderHeight - 1);

            using (var pen = new Pen(Theme.GridLine))
            {
                x = _nameWidth;
                for (int i = 0; i < _cols.Count - 1; i++)
                {
                    x += _widths[i];
                    g.DrawLine(pen, x, 6, x, HeaderHeight - 4);
                }
            }
        }

        private void DrawColumnLines(Graphics g)
        {
            using (var pen = new Pen(Theme.GridLine))
            {
                int x = _nameWidth;
                for (int i = 0; i < _cols.Count - 1; i++)
                {
                    x += _widths[i];
                    g.DrawLine(pen, x, HeaderHeight, x, Height);
                }
            }
        }

        private void DrawRow(Graphics g, HwNode node, int index, int y, int contentWidth)
        {
            bool isGroup = node.Kind == HwNodeKind.Group;
            bool selected = node == _selected;
            bool hover = index == _hoverRow && !selected;

            Color back = selected ? Theme.Selection : (isGroup ? Theme.GroupBack : Theme.Surface);
            if (hover) back = Theme.Blend(back, Theme.Text, 0.035);

            using (var brush = new SolidBrush(back))
                g.FillRectangle(brush, 0, y, contentWidth, RowHeight);

            if (isGroup || selected)
            {
                using (var brush = new SolidBrush(Theme.Accent))
                    g.FillRectangle(brush, 0, y, 3, RowHeight);
            }

            int x0 = 10 + node.Depth * Indent;
            if (node.HasChildren)
                DrawGlyph(g, x0, y + RowHeight / 2, node.Expanded,
                          selected ? Theme.Text : (isGroup ? Theme.Accent : Theme.TextDim));

            int textX = x0 + 14;
            Font font = isGroup ? Fonts.BaseBold : Font;
            Color textColor = node.Kind == HwNodeKind.Sensor ? Theme.TextSensor : Theme.Text;
            TextRenderer.DrawText(g, node.Text, font,
                new Rectangle(textX, y, Math.Max(0, _nameWidth - textX - 6), RowHeight),
                textColor, Flags(HorizontalAlignment.Left));

            int x = _nameWidth;
            for (int i = 0; i < _cols.Count; i++)
            {
                HwColumnDef column = _cols[i];
                string value = node.GetValue(i);
                if (value.Length > 0)
                {
                    Rectangle cell = new Rectangle(x, y, _widths[i], RowHeight);
                    if (column.Bar) DrawBar(g, cell, value, back);

                    HwLevel level = node.GetLevel(i);
                    Color color = level != HwLevel.Normal
                                      ? Theme.Level(level)
                                      : (column.Accent ? Theme.Accent : Theme.Text);

                    int pad = column.Align == HorizontalAlignment.Left ? 8 : 6;
                    int textOffset = column.Bar ? -4 : 0;
                    TextRenderer.DrawText(g, value, Font,
                        new Rectangle(cell.X + pad, cell.Y + textOffset, cell.Width - pad * 2, cell.Height),
                        color, Flags(column.Align));
                }
                x += _widths[i];
            }
        }

        private static void DrawBar(Graphics g, Rectangle cell, string value, Color back)
        {
            string text = value.Trim();
            int space = text.IndexOf(' ');
            if (space > 0) text = text.Substring(0, space);

            double percent;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out percent)) return;
            percent = Math.Max(0, Math.Min(100, percent));

            // Дорожка у нижней кромки ячейки + заливка по значению: аккуратная
            // «шкала», которая не спорит с right-aligned числом.
            int trackX = cell.X + 7;
            int trackWidth = Math.Max(12, cell.Width - 14);
            int trackY = cell.Bottom - 7;

            using (var track = new SolidBrush(Theme.GridLine))
                g.FillRectangle(track, trackX, trackY, trackWidth, 3);

            int fill = (int)Math.Round(trackWidth * percent / 100.0);
            if (fill <= 0) return;

            using (var brush = new SolidBrush(Theme.Blend(back, Theme.Accent, 0.85)))
                g.FillRectangle(brush, trackX, trackY, fill, 3);
        }

        private static void DrawGlyph(Graphics g, int x, int centerY, bool expanded, Color color)
        {
            const int size = 4;
            Point[] points = expanded
                ? new[]
                  {
                      new Point(x - 1, centerY - size / 2),
                      new Point(x + 2 * size - 1, centerY - size / 2),
                      new Point(x + size - 1, centerY + size / 2 + 1)
                  }
                : new[]
                  {
                      new Point(x, centerY - size),
                      new Point(x, centerY + size),
                      new Point(x + size + 1, centerY)
                  };

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, points);
            g.SmoothingMode = old;
        }

        // ---------- mouse ----------

        private HwNode NodeAt(int y)
        {
            if (y < HeaderHeight) return null;
            int index = (_scroll.Visible ? _scroll.Value : 0) + (y - HeaderHeight) / RowHeight;
            return index >= 0 && index < _rows.Count ? _rows[index] : null;
        }

        private int IndexAt(int y)
        {
            if (y < HeaderHeight) return -1;
            int index = (_scroll.Visible ? _scroll.Value : 0) + (y - HeaderHeight) / RowHeight;
            return index >= 0 && index < _rows.Count ? index : -1;
        }

        private bool OnGlyph(HwNode node, int x)
        {
            int x0 = 10 + node.Depth * Indent;
            return x >= x0 - 5 && x <= x0 + 12;
        }

        private void ToggleNode(HwNode node)
        {
            node.Expanded = !node.Expanded;
            RebuildRows();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Y);
            if (index != _hoverRow)
            {
                _hoverRow = index;
                Invalidate();
            }

            HwNode node = index >= 0 ? _rows[index] : null;
            Cursor = node != null && node.HasChildren && OnGlyph(node, e.X) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverRow != -1)
            {
                _hoverRow = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            HwNode node = NodeAt(e.Y);
            if (node == null) return;

            _selected = node;
            if (e.Button == MouseButtons.Left && node.HasChildren && OnGlyph(node, e.X))
            {
                ToggleNode(node);
                return;
            }
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            HwNode node = NodeAt(e.Y);
            if (node != null && node.HasChildren && !OnGlyph(node, e.X)) ToggleNode(node);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (CanScroll)
            {
                _scroll.Value -= Math.Sign(e.Delta) * 3;
                var handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
            }
            else
            {
                base.OnMouseWheel(e);
            }
        }

        // ---------- keyboard ----------

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
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
            if (_rows.Count == 0) return;

            int index = _selected == null ? -1 : _rows.IndexOf(_selected);
            int visible = VisibleRows;
            bool filtering = _filter.Length > 0;
            HwNode current = index >= 0 ? _rows[index] : null;

            switch (e.KeyCode)
            {
                case Keys.Down: index = Math.Min(_rows.Count - 1, index + 1); break;
                case Keys.Up: index = Math.Max(0, index - 1); break;
                case Keys.PageDown: index = Math.Min(_rows.Count - 1, index + visible); break;
                case Keys.PageUp: index = Math.Max(0, index - visible); break;
                case Keys.Home: index = 0; break;
                case Keys.End: index = _rows.Count - 1; break;
                case Keys.Right:
                    if (current != null && current.HasChildren && !current.Expanded && !filtering)
                    {
                        ToggleNode(current);
                        e.Handled = true;
                        return;
                    }
                    index = Math.Min(_rows.Count - 1, index + 1);
                    break;
                case Keys.Left:
                    if (current != null && current.HasChildren && current.Expanded && !filtering)
                    {
                        ToggleNode(current);
                        e.Handled = true;
                        return;
                    }
                    if (current != null && current.Parent != null) index = _rows.IndexOf(current.Parent);
                    break;
                case Keys.Space:
                case Keys.Enter:
                    if (current != null && current.HasChildren && !filtering) ToggleNode(current);
                    e.Handled = true;
                    return;
                default:
                    return;
            }

            e.Handled = true;
            if (index < 0) index = 0;
            _selected = _rows[index];
            EnsureVisible(index);
            Invalidate();
        }
    }
}
