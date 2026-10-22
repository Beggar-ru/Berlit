using System;
using System.Collections.Generic;
using System.Globalization;

namespace Berlit
{
    public enum HwNodeKind
    {
        Group,      // CPU, Memory, GPU, Storage, Mainboard
        Component,  // конкретное устройство или подгруппа (Voltages, Fans...)
        Sensor      // строка с данными
    }

    /// <summary>
    /// Важность значения. Влияет только на цвет в HwTreeGrid:
    /// нормальное, приглушённое ("н/д"), предупреждение, критичное или акцент.
    /// </summary>
    public enum HwLevel
    {
        Normal,
        Dim,
        Warn,
        Critical,
        Accent
    }

    /// <summary>
    /// Индексы колонок значений. Порядок совпадает с колонками в HwTreeGrid
    /// (см. BuildHardwareColumns в Berlit.cs).
    /// </summary>
    public static class HwCol
    {
        public const int Load = 0;
        public const int TempMin = 1;
        public const int Temp = 2;
        public const int TempMax = 3;
        public const int FanRpm = 4;
        public const int Clock = 5;
        public const int Voltage = 6;
        public const int Power = 7;
        public const int Info = 8;      // текст: объём, модель, статус
        public const int MemClock = 9;  // частота памяти видеокарты
        public const int Count = 10;
    }

    /// <summary>
    /// Узел дерева мониторинга. Количество узлов не ограничено:
    /// несколько дисков / видеокарт просто добавляются как новые Component.
    /// </summary>
    public sealed class HwNode
    {
        private readonly List<HwNode> _children = new List<HwNode>();
        private readonly string[] _values = new string[HwCol.Count];
        private readonly HwLevel[] _levels = new HwLevel[HwCol.Count];

        public HwNode(string text, HwNodeKind kind)
        {
            Text = text;
            Kind = kind;
            for (int i = 0; i < _values.Length; i++) _values[i] = string.Empty;
        }

        public string Text { get; set; }
        public HwNodeKind Kind { get; set; }
        public bool Expanded { get; set; }
        public HwNode Parent { get; private set; }

        /// <summary>Колонки раздела: каждый верхний раздел задаёт свой набор и порядок.</summary>
        public List<HwColumnDef> Columns { get; set; }

        public IList<HwNode> Children { get { return _children; } }
        public bool HasChildren { get { return _children.Count > 0; } }

        public int Depth
        {
            get
            {
                int d = 0;
                HwNode p = Parent;
                while (p != null) { d++; p = p.Parent; }
                return d;
            }
        }

        public string Path
        {
            get { return Parent == null ? Text : Parent.Path + "/" + Text; }
        }

        public string GetValue(int col)
        {
            return (col >= 0 && col < _values.Length) ? _values[col] : string.Empty;
        }

        public HwLevel GetLevel(int col)
        {
            return (col >= 0 && col < _levels.Length) ? _levels[col] : HwLevel.Normal;
        }

        public HwNode Set(int col, string value)
        {
            return Set(col, value, HwLevel.Normal);
        }

        public HwNode Set(int col, string value, HwLevel level)
        {
            if (col >= 0 && col < _values.Length)
            {
                _values[col] = value ?? string.Empty;
                _levels[col] = level;
            }
            return this;
        }

        /// <summary>Добавляет дочерний узел и возвращает его.</summary>
        public HwNode Add(HwNode child)
        {
            child.Parent = this;
            _children.Add(child);
            return child;
        }

        /// <summary>Добавляет дочерний узел и возвращает родителя — удобно для цепочек.</summary>
        public HwNode AddRange(IEnumerable<HwNode> children)
        {
            if (children == null) return this;
            foreach (HwNode child in children) Add(child);
            return this;
        }
    }

    /// <summary>Форматирование значений датчиков. Единицы — часть строки.</summary>
    public static class HwFormat
    {
        private static string F(double v, string fmt)
        {
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        public static string Percent(double v)
        {
            return F(Math.Max(0, Math.Min(100, v)), "0") + " %";
        }

        public static string Celsius(double v) { return F(v, "0") + " \u00B0C"; }

        public static string Celsius1(double v) { return F(v, "0.0") + " \u00B0C"; }

        public static string Rpm(double v) { return F(v, "0"); }

        public static string Mhz(double v) { return F(v, "0"); }

        public static string Ghz(double mhz)
        {
            return mhz >= 999.5 ? F(mhz / 1000.0, "0.00") + " GHz" : F(mhz, "0") + " MHz";
        }

        public static string Volt(double v) { return F(v, "0.000") + " V"; }

        public static string Watt(double v) { return F(v, "0.0") + " W"; }

        public static string Gb(double v) { return F(v, "0.0") + " GB"; }

        /// <summary>Байты в человекочитаемый вид (B / MB / GB / TB).</summary>
        public static string Bytes(long bytes)
        {
            if (bytes < 0) return "\u2014";
            const double kb = 1024.0, mb = kb * 1024, gb = mb * 1024, tb = gb * 1024;
            if (bytes >= tb) return F(bytes / tb, "0.00") + " TB";
            if (bytes >= gb) return F(bytes / gb, "0.0") + " GB";
            if (bytes >= mb) return F(bytes / mb, "0") + " MB";
            if (bytes >= kb) return F(bytes / kb, "0") + " KB";
            return F(bytes, "0") + " B";
        }

        public static string Uptime(TimeSpan up)
        {
            if (up.TotalDays >= 1)
                return string.Format(CultureInfo.InvariantCulture, "{0} d {1:00}:{2:00}", (int)up.TotalDays, up.Hours, up.Minutes);
            return string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min", up.Hours, up.Minutes);
        }
    }
}
