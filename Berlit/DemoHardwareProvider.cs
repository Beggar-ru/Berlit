// Демо-провайдер с ФЕЙКОВЫМИ данными. Отключён: по умолчанию проект собирается
// с реальным SystemHardwareProvider. Чтобы вернуть демо-режим для проверки вёрстки,
// добавьте BERLIT_DEMO в DefineConstants проекта (Berlit.csproj) и выберите
// провайдер в Berlit.cs (CreateProvider).
#if BERLIT_DEMO

using System;
using System.Collections.Generic;

namespace Berlit
{
    /// <summary>
    /// Демо-реализация со случайными значениями — только для проверки масштабирования
    /// вёрстки (2 видеоядра, несколько дисков, много ядер). В обычной сборке не участвует.
    /// </summary>
    public sealed class DemoHardwareProvider : IHardwareProvider
    {
        private readonly Random _rnd = new Random();
        private readonly Dictionary<string, double> _min = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _max = new Dictionary<string, double>();

        public string Status { get { return "DEMO DATA - not real hardware"; } }

        public string Summary { get { return "CPU 12 %     RAM 11.2 / 32.0 GB     GPU 18 %"; } }

        public HudSnapshot GetHudSnapshot()
        {
            return new HudSnapshot
            {
                CpuLoad = 12,
                CpuTemp = 45,
                HasGpu = true,
                GpuLoad = 18,
                GpuTemp = 52,
                RamPercent = 35
            };
        }

        private double Jitter(double baseValue, double amplitude)
        {
            return baseValue + (_rnd.NextDouble() * 2 - 1) * amplitude;
        }

        private void Track(string key, double v, out double min, out double max)
        {
            double lo;
            if (!_min.TryGetValue(key, out lo) || v < lo) lo = v;
            _min[key] = lo;

            double hi;
            if (!_max.TryGetValue(key, out hi) || v > hi) hi = v;
            _max[key] = hi;

            min = lo;
            max = hi;
        }

        private HwNode SetTemp(HwNode node, string key, double v)
        {
            double lo, hi;
            Track(key, v, out lo, out hi);
            return node.Set(HwCol.TempMin, HwFormat.Celsius(lo))
                       .Set(HwCol.Temp, HwFormat.Celsius(v))
                       .Set(HwCol.TempMax, HwFormat.Celsius(hi));
        }

        private static HwNode Device(string name)
        {
            return new HwNode(name, HwNodeKind.Component) { Expanded = true };
        }

        private static HwNode Sensor(HwNode parent, string name)
        {
            return parent.Add(new HwNode(name, HwNodeKind.Sensor));
        }

        public IList<HwNode> GetSystemNodes()
        {
            var list = new List<HwNode>();
            HwNode pc = Device("DEMO-PC");
            Sensor(pc, "Operating system").Set(HwCol.Info, "Windows (demo)");
            Sensor(pc, "Uptime").Set(HwCol.Info, "3 d 04:12");
            list.Add(pc);
            return list;
        }

        public IList<HwNode> GetCpuNodes()
        {
            var list = new List<HwNode>();
            HwNode cpu = Device("Demo CPU 8C/16T");

            for (int i = 0; i < 8; i++)
            {
                HwNode core = Sensor(cpu, "Core #" + i);
                core.Set(HwCol.Load, HwFormat.Percent(Jitter(12 + i * 3, 8)));
                SetTemp(core, "cpu.core" + i, Jitter(38 + i, 3));
                core.Set(HwCol.Clock, HwFormat.Mhz(Jitter(3600, 150)));
            }

            HwNode pkg = Sensor(cpu, "Package");
            pkg.Set(HwCol.Load, HwFormat.Percent(Jitter(20, 8)));
            SetTemp(pkg, "cpu.pkg", Jitter(45, 2));
            pkg.Set(HwCol.Voltage, HwFormat.Volt(Jitter(1.12, 0.02)));
            pkg.Set(HwCol.Power, HwFormat.Watt(Jitter(48, 6)));

            list.Add(cpu);
            return list;
        }

        public IList<HwNode> GetMemoryNodes()
        {
            var list = new List<HwNode>();

            HwNode ram = Device("Physical memory");
            double used = Jitter(11.2, 0.3);
            HwNode usage = Sensor(ram, "Memory used");
            usage.Set(HwCol.Load, HwFormat.Percent(used / 32.0 * 100))
                 .Set(HwCol.Info, HwFormat.Gb(used) + " / " + HwFormat.Gb(32));

            for (int i = 1; i <= 2; i++)
            {
                HwNode dimm = Sensor(ram, "DIMM A" + i);
                SetTemp(dimm, "ram.dimm" + i, Jitter(34, 1.5));
                dimm.Set(HwCol.Clock, HwFormat.Mhz(3200))
                    .Set(HwCol.Info, "16.0 GB DDR4");
            }
            list.Add(ram);

            return list;
        }

        public IList<HwNode> GetGpuNodes()
        {
            var list = new List<HwNode>();

            HwNode igpu = Device("Integrated GPU (demo)");
            HwNode icore = Sensor(igpu, "GPU Core");
            icore.Set(HwCol.Load, HwFormat.Percent(Jitter(6, 4)));
            SetTemp(icore, "igpu.core", Jitter(41, 2));
            icore.Set(HwCol.Clock, HwFormat.Mhz(Jitter(350, 30)));
            list.Add(igpu);

            HwNode dgpu = Device("Discrete GPU (demo)");
            HwNode core = Sensor(dgpu, "GPU Core");
            core.Set(HwCol.Load, HwFormat.Percent(Jitter(18, 10)));
            SetTemp(core, "dgpu.core", Jitter(46, 2));
            core.Set(HwCol.Clock, HwFormat.Mhz(Jitter(1500, 60)));
            core.Set(HwCol.Voltage, HwFormat.Volt(Jitter(0.85, 0.01)));
            core.Set(HwCol.Power, HwFormat.Watt(Jitter(65, 8)));

            HwNode mem = Sensor(dgpu, "GPU Memory");
            mem.Set(HwCol.Load, HwFormat.Percent(Jitter(26, 3)))
               .Set(HwCol.Clock, HwFormat.Mhz(7000))
               .Set(HwCol.Info, "2.1 / 8.0 GB");

            Sensor(dgpu, "GPU Fan").Set(HwCol.FanRpm, HwFormat.Rpm(Jitter(1100, 40)));
            list.Add(dgpu);

            return list;
        }

        public IList<HwNode> GetStorageNodes()
        {
            var list = new List<HwNode>();
            string[] names = { "Demo NVMe SSD 1TB", "Demo SATA SSD 512GB", "Demo HDD 4TB" };
            double[] baseTemp = { 38, 33, 36 };
            string[] health = { "Good, 98 % life left", "Good, 91 % life left", "Good" };
            string[] hours = { "1 234 h", "6 812 h", "21 540 h" };
            double[] usedPct = { 62, 41, 83 };

            for (int d = 0; d < names.Length; d++)
            {
                HwNode disk = Device(names[d]);

                HwNode temp = Sensor(disk, "Temperature");
                SetTemp(temp, "disk" + d + ".temp", Jitter(baseTemp[d], 1.5));

                Sensor(disk, "Used space").Set(HwCol.Load, HwFormat.Percent(usedPct[d]));
                Sensor(disk, "SMART: health").Set(HwCol.Info, health[d]);
                Sensor(disk, "SMART: power-on hours").Set(HwCol.Info, hours[d]);
                Sensor(disk, "SMART: reallocated sectors").Set(HwCol.Info, "0");

                list.Add(disk);
            }
            return list;
        }

        public IList<HwNode> GetMainboardNodes()
        {
            var list = new List<HwNode>();
            HwNode board = Device("Demo Mainboard");

            HwNode volts = board.Add(new HwNode("Voltages", HwNodeKind.Component) { Expanded = true });
            Sensor(volts, "+12V").Set(HwCol.Voltage, HwFormat.Volt(Jitter(12.05, 0.03)));
            Sensor(volts, "+5V").Set(HwCol.Voltage, HwFormat.Volt(Jitter(5.02, 0.02)));
            Sensor(volts, "+3.3V").Set(HwCol.Voltage, HwFormat.Volt(Jitter(3.31, 0.01)));
            Sensor(volts, "Vcore").Set(HwCol.Voltage, HwFormat.Volt(Jitter(1.12, 0.02)));

            HwNode fans = board.Add(new HwNode("Fans", HwNodeKind.Component) { Expanded = true });
            Sensor(fans, "CPU_FAN").Set(HwCol.FanRpm, HwFormat.Rpm(Jitter(1250, 40)));
            Sensor(fans, "CHA_FAN1").Set(HwCol.FanRpm, HwFormat.Rpm(Jitter(900, 30)));

            HwNode temps = board.Add(new HwNode("Temperatures", HwNodeKind.Component) { Expanded = true });
            SetTemp(Sensor(temps, "System"), "mb.sys", Jitter(32, 1));
            SetTemp(Sensor(temps, "VRM"), "mb.vrm", Jitter(44, 2));

            list.Add(board);
            return list;
        }

        public void Dispose()
        {
        }
    }
}

#endif
