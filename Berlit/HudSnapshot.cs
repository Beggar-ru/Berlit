using System;

namespace Berlit
{
    /// <summary>
    /// Компактный снимок для оверлея поверх всех окон.
    /// </summary>
    public sealed class HudSnapshot
    {
        public double CpuLoad;
        public double? CpuTemp;

        public bool HasGpu;
        public double? GpuLoad;
        public double? GpuTemp;

        public double RamPercent;
    }
}
