using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Berlit
{
    /// <summary>Описание колонки таблицы: вес распределяет свободную ширину.</summary>
    public sealed class GridColumnDef
    {
        public GridColumnDef(string header, float weight, HorizontalAlignment align, int minWidth = 56)
        {
            Header = header;
            Weight = weight;
            Align = align;
            MinWidth = minWidth;
        }

        public string Header { get; private set; }
        public float Weight { get; private set; }
        public HorizontalAlignment Align { get; private set; }
        public int MinWidth { get; private set; }
    }

    /// <summary>Строка таблицы. Tag нужен, чтобы сохранить выделение между обновлениями.</summary>
    public sealed class GridRow
    {
        public GridRow(string[] cells, object tag = null)
        {
            Cells = cells ?? new string[0];
            Levels = new HwLevel[Cells.Length];
            Tag = tag;
        }

        public string[] Cells { get; private set; }
        public HwLevel[] Levels { get; private set; }
        public object Tag { get; set; }

        public string Get(int index)
        {
            return index >= 0 && index < Cells.Length ? (Cells[index] ?? string.Empty) : string.Empty;
        }

        public HwLevel Level(int index)
        {
            return index >= 0 && index < Levels.Length ? Levels[index] : HwLevel.Normal;
        }

        public GridRow SetLevel(int index, HwLevel level)
        {
            if (index >= 0 && index < Levels.Length) Levels[index] = level;
            return this;
        }
    }

    public sealed class ColumnClickEventArgs : EventArgs
    {
        public ColumnClickEventArgs(int column)
        {
            Column = column;
        }

        public int Column { get; private set; }
    }

    /// <summary>
    /// Простая таблица в стиле дерева датчиков: та же шапка, та же тёмная
    /// полоса прокрутки, сортировка по клику на заголовок. Используется для
    /// служб и процессов.
    /// </summary>
    public sealed class DataTableGrid : Control
    {
        private const int HeaderHeight = 27;
        private const int RowHeight = 22;

        private readonly List<GridColumnDef> _cols = new List<GridColumnDef>();
        private readonly List<GridRow> _rows = new List<GridRow>();
        private readonly DarkScrollBar _scroll = new DarkScrollBar();
        private int[] _widths = new int[0];

        private int _hoverRow = -1;
        private int _selectedIndex = -1;
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        public DataTableGrid()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Surface;
            Font = Fonts.Base;

            _scroll.Dock = DockStyle.Right;
            _scroll.Visible = false;
            _scroll.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_scroll);
        }

        public event EventHandler<ColumnClickEventArgs> ColumnHeaderClicked;

        // ---------- public API ----------

        public void SetColumns(IEnumerable<GridColumnDef> columns)
        {
            _cols.Clear();
            _cols.AddRange(columns);
            _widths = new int[_cols.Count];
            Invalidate();
        }

        public void SetRows(IEnumerable<GridRow> rows)
        {
            object selectedTag = SelectedRow != null ? SelectedRow.Tag : null;

            _rows.Clear();
            _rows.AddRange(rows);

            _selectedIndex = -1;
            if (selectedTag != null)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (Equals(_rows[i].Tag, selectedTag))
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }

            if (_hoverRow >= _rows.Count) _hoverRow = -1;
            UpdateScroll();
            Invalidate();
        }

        public GridRow SelectedRow
        {
            get { return _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null; }
        }

        public int SortColumn
        {
            get { return _sortColumn; }
            set { _sortColumn = value; Invalidate(); }
        }

        public bool SortAscending
        {
            get { return _sortAscending; }
            set { _sortAscending = value; Invalidate(); }
        }

        public int RowCount { get { return _rows.Count; } }

        // ---------- layout / scroll ----------

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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        private void LayoutColumns(int contentWidth)
        {
            if (_widths.Length != _cols.Count) _widths = new int[_cols.Count];
            if (_cols.Count == 0) return;

            float totalWeight = 0;
            foreach (GridColumnDef column in _cols) totalWeight += column.Weight;
            if (totalWeight <= 0) totalWeight = 1;

            int used = 0;
            for (int i = 0; i < _cols.Count; i++)
            {
                int width = (int)Math.Round(contentWidth * _cols[i].Weight / totalWeight);
                width = Math.Max(_cols[i].MinWidth, width);
                _widths[i] = width;
                used += width;
            }

            // Остаток (или перебор) отдаём последней колонке.
            int last = _cols.Count - 1;
            _widths[last] = Math.Max(_cols[last].MinWidth, _widths[last] + (contentWidth - used));
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

            using (var pen = new Pen(Theme.GridLine))
            {
                int x = 0;
                for (int i = 0; i < _cols.Count - 1; i++)
                {
                    x += _widths[i];
                    g.DrawLine(pen, x, HeaderHeight, x, Height);
                }
            }

            if (_rows.Count == 0)
            {
                TextRenderer.DrawText(g, "No data", Font,
                    new Rectangle(0, HeaderHeight, contentWidth, Math.Max(0, Height - HeaderHeight)),
                    Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawHeader(Graphics g, int contentWidth)
        {
            using (var back = new SolidBrush(Theme.SurfaceAlt))
                g.FillRectangle(back, 0, 0, Width, HeaderHeight);

            int x = 0;
            for (int i = 0; i < _cols.Count; i++)
            {
                GridColumnDef column = _cols[i];
                int pad = column.Align == HorizontalAlignment.Left ? 10 : 8;
                TextRenderer.DrawText(g, column.Header, Fonts.Header,
                    new Rectangle(x + pad, 0, Math.Max(0, _widths[i] - pad * 2), HeaderHeight),
                    Theme.TextDim, Flags(column.Align));

                if (i == _sortColumn) DrawSortGlyph(g, x + _widths[i] - 14, HeaderHeight / 2, _sortAscending);
                x += _widths[i];
            }

            using (var pen = new Pen(Theme.Accent, 2))
                g.DrawLine(pen, 0, HeaderHeight - 1, contentWidth, HeaderHeight - 1);

            using (var pen = new Pen(Theme.GridLine))
            {
                x = 0;
                for (int i = 0; i < _cols.Count - 1; i++)
                {
                    x += _widths[i];
                    g.DrawLine(pen, x, 6, x, HeaderHeight - 4);
                }
            }
        }

        private static void DrawSortGlyph(Graphics g, int x, int centerY, bool ascending)
        {
            Point[] points = ascending
                ? new[]
                  {
                      new Point(x, centerY + 3),
                      new Point(x + 8, centerY + 3),
                      new Point(x + 4, centerY - 3)
                  }
                : new[]
                  {
                      new Point(x, centerY - 3),
                      new Point(x + 8, centerY - 3),
                      new Point(x + 4, centerY + 3)
                  };

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Theme.Accent))
                g.FillPolygon(brush, points);
            g.SmoothingMode = old;
        }

        private void DrawRow(Graphics g, GridRow row, int index, int y, int contentWidth)
        {
            bool selected = index == _selectedIndex;
            bool hover = index == _hoverRow && !selected;

            Color back = selected ? Theme.Selection : (hover ? Theme.Hover : Theme.Surface);
            using (var brush = new SolidBrush(back))
                g.FillRectangle(brush, 0, y, contentWidth, RowHeight);

            if (selected)
            {
                using (var brush = new SolidBrush(Theme.Accent))
                    g.FillRectangle(brush, 0, y, 3, RowHeight);
            }

            int x = 0;
            for (int i = 0; i < _cols.Count; i++)
            {
                GridColumnDef column = _cols[i];
                string value = row.Get(i);
                if (value.Length > 0)
                {
                    int pad = column.Align == HorizontalAlignment.Left ? 10 : 8;
                    HwLevel level = row.Level(i);
                    Color color = level != HwLevel.Normal ? Theme.Level(level) : Theme.Text;
                    TextRenderer.DrawText(g, value, Font,
                        new Rectangle(x + pad, y, Math.Max(0, _widths[i] - pad * 2), RowHeight),
                        color, Flags(column.Align));
                }
                x += _widths[i];
            }
        }

        // ---------- mouse ----------

        private int RowAt(int y)
        {
            if (y < HeaderHeight) return -1;
            int index = (_scroll.Visible ? _scroll.Value : 0) + (y - HeaderHeight) / RowHeight;
            return index >= 0 && index < _rows.Count ? index : -1;
        }

        private int ColumnAt(int x)
        {
            int left = 0;
            for (int i = 0; i < _cols.Count; i++)
            {
                if (x >= left && x < left + _widths[i]) return i;
                left += _widths[i];
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = RowAt(e.Y);
            if (index != _hoverRow)
            {
                _hoverRow = index;
                Invalidate();
            }
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

            if (e.Y < HeaderHeight)
            {
                int column = ColumnAt(e.X);
                if (column >= 0)
                {
                    var handler = ColumnHeaderClicked;
                    if (handler != null) handler(this, new ColumnClickEventArgs(column));
                }
                return;
            }

            int index = RowAt(e.Y);
            if (index >= 0)
            {
                _selectedIndex = index;
                Invalidate();
            }
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

            int index = _selectedIndex;
            int visible = VisibleRows;

            switch (e.KeyCode)
            {
                case Keys.Down: index = Math.Min(_rows.Count - 1, index + 1); break;
                case Keys.Up: index = Math.Max(0, index - 1); break;
                case Keys.PageDown: index = Math.Min(_rows.Count - 1, index + visible); break;
                case Keys.PageUp: index = Math.Max(0, index - visible); break;
                case Keys.Home: index = 0; break;
                case Keys.End: index = _rows.Count - 1; break;
                default: return;
            }

            e.Handled = true;
            _selectedIndex = index;
            if (CanScroll)
            {
                int top = _scroll.Value;
                if (index < top) _scroll.Value = index;
                else if (index >= top + visible) _scroll.Value = index - visible + 1;
            }
            Invalidate();
        }
    }
}
