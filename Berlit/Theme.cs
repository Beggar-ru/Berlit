using System.Drawing;

namespace Berlit
{
    /// <summary>
    /// Единая палитра: тёмные тона (чёрный/серый) + бирюзовый акцент.
    /// Все цвета интерфейса берутся только отсюда, чтобы тема не расползалась
    /// по контролам. Акцент используется сдержанно: заголовки, выделение,
    /// важные значения, полоски загрузки.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Back       = Color.FromArgb(24, 24, 24);   // фон окна / полоса вкладок
        public static readonly Color Surface    = Color.FromArgb(34, 34, 34);   // фон страниц, таблиц
        public static readonly Color SurfaceAlt = Color.FromArgb(44, 44, 44);   // шапки, панели инструментов
        public static readonly Color GroupBack  = Color.FromArgb(46, 46, 46);   // строки верхнего уровня (CPU, GPU...)
        public static readonly Color GridLine   = Color.FromArgb(52, 52, 52);   // разделители колонок
        public static readonly Color Border     = Color.FromArgb(78, 78, 78);
        public static readonly Color Hover      = Color.FromArgb(38, 38, 38);   // подсветка строки под курсором

        public static readonly Color Text       = Color.FromArgb(226, 226, 226);
        public static readonly Color TextSensor = Color.FromArgb(190, 190, 190);
        public static readonly Color TextDim    = Color.FromArgb(150, 150, 150);
        public static readonly Color TextFaint  = Color.FromArgb(112, 112, 112);

        public static readonly Color Accent     = Color.FromArgb(0, 188, 175);  // бирюзовый
        public static readonly Color AccentDark = Color.FromArgb(0, 105, 98);
        public static readonly Color Selection  = Color.FromArgb(0, 52, 49);

        public static readonly Color Warn       = Color.FromArgb(226, 168, 74); // предупреждение
        public static readonly Color Danger     = Color.FromArgb(214, 86, 78);  // ошибка / критично

        public static readonly Color Button     = Color.FromArgb(50, 50, 50);
        public static readonly Color ButtonHot  = Color.FromArgb(62, 62, 62);
        public static readonly Color Scroll     = Color.FromArgb(66, 66, 66);
        public static readonly Color ScrollHot  = Color.FromArgb(102, 102, 102);

        /// <summary>Смешивает два цвета (t = 0 -> back, t = 1 -> fore).</summary>
        public static Color Blend(Color back, Color fore, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(back.R + (fore.R - back.R) * t),
                (int)(back.G + (fore.G - back.G) * t),
                (int)(back.B + (fore.B - back.B) * t));
        }

        public static Color Level(HwLevel level)
        {
            switch (level)
            {
                case HwLevel.Warn: return Warn;
                case HwLevel.Critical: return Danger;
                case HwLevel.Dim: return TextDim;
                case HwLevel.Accent: return Accent;
                default: return Text;
            }
        }
    }

    /// <summary>
    /// Шрифты приложения. Создаются один раз на процесс и не освобождаются:
    /// живут столько же, сколько окно.
    /// </summary>
    internal static class Fonts
    {
        public static readonly Font Base      = new Font("Segoe UI", 9F);
        public static readonly Font BaseBold  = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font Title     = new Font("Segoe UI", 12F, FontStyle.Bold);
        public static readonly Font Caption   = new Font("Segoe UI", 7.5F);
        public static readonly Font Header    = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        public static readonly Font Small     = new Font("Segoe UI", 8.25F);
    }
}
