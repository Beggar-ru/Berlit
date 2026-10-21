using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace Berlit
{
    /// <summary>
    /// Загрузка процессора и текущая частота. Источник — счётчики производительности
    /// Windows ("Processor", "Processor Information"), права администратора не нужны.
    /// </summary>
    internal sealed class CpuSampler : IDisposable
    {
        private readonly List<PerformanceCounter> _disposables = new List<PerformanceCounter>();
        private PerformanceCounter _total;
        private PerformanceCounter _perf;
        private PerformanceCounter[] _cores = new PerformanceCounter[0];

        public double BaseClockMhz { get; private set; }
        public double TotalLoad { get; private set; }
        public double FrequencyMhz { get; private set; }
        public double[] CoreLoads { get; private set; }
        public bool HasLoad { get { return _total != null; } }
        public bool HasFrequency { get { return _perf != null && BaseClockMhz > 0; } }

        public CpuSampler(double baseClockMhz)
        {
            BaseClockMhz = baseClockMhz;
            FrequencyMhz = baseClockMhz;
            CoreLoads = new double[0];

            try
            {
                _total = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _total.NextValue();               // первый замер всегда 0 — «праймим» счётчик
                _disposables.Add(_total);
            }
            catch
            {
                _total = null;
            }

            try
            {
                _perf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
                _perf.NextValue();
                _disposables.Add(_perf);
            }
            catch
            {
                _perf = null;
            }

            try
            {
                var category = new PerformanceCounterCategory("Processor");
                var instances = new List<string>();
                foreach (string name in category.GetInstanceNames())
                {
                    int index;
                    if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                        instances.Add(name);
                }
                instances.Sort(delegate (string a, string b)
                {
                    return int.Parse(a, CultureInfo.InvariantCulture).CompareTo(int.Parse(b, CultureInfo.InvariantCulture));
                });

                var counters = new List<PerformanceCounter>();
                foreach (string name in instances)
                {
                    var counter = new PerformanceCounter("Processor", "% Processor Time", name, true);
                    counter.NextValue();
                    counters.Add(counter);
                    _disposables.Add(counter);
                }
                _cores = counters.ToArray();
            }
            catch
            {
                _cores = new PerformanceCounter[0];
            }

            // Даём счётчикам накопить дельту, иначе первый опрос покажет мусор.
            if (_total != null && _cores.Length > 0) Thread.Sleep(120);
        }

        private static double Read(PerformanceCounter counter)
        {
            if (counter == null) return 0;
            try
            {
                double value = counter.NextValue();
                return double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(100, value));
            }
            catch
            {
                return 0;
            }
        }

        public void Sample()
        {
            TotalLoad = Read(_total);

            if (_perf != null && BaseClockMhz > 0)
            {
                try
                {
                    double percent = _perf.NextValue();
                    if (percent > 0 && !double.IsNaN(percent) && !double.IsInfinity(percent))
                        FrequencyMhz = BaseClockMhz * percent / 100.0;
                }
                catch
                {
                }
            }

            if (_cores.Length > 0)
            {
                var loads = new double[_cores.Length];
                for (int i = 0; i < _cores.Length; i++) loads[i] = Read(_cores[i]);
                CoreLoads = loads;
            }
        }

        public void Dispose()
        {
            foreach (PerformanceCounter counter in _disposables)
            {
                try { counter.Dispose(); }
                catch { }
            }
            _disposables.Clear();
            _total = null;
            _perf = null;
            _cores = new PerformanceCounter[0];
        }
    }

    /// <summary>
    /// Температуры ACPI-зон через счётчики "Thermal Zone Information".
    /// Работает без прав администратора и без драйверов; набор зон зависит от платы.
    /// </summary>
    internal sealed class ThermalSampler : IDisposable
    {
        private PerformanceCounter[] _counters = new PerformanceCounter[0];
        private string[] _names = new string[0];
        private bool _initialized;

        public bool Available { get; private set; }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var category = new PerformanceCounterCategory("Thermal Zone Information");
                var names = new List<string>();
                var counters = new List<PerformanceCounter>();

                foreach (string instance in category.GetInstanceNames())
                {
                    try
                    {
                        var counter = new PerformanceCounter("Thermal Zone Information", "Temperature", instance, true);
                        counter.NextValue();
                        counters.Add(counter);
                        names.Add(CleanName(instance));
                    }
                    catch
                    {
                    }
                }

                _counters = counters.ToArray();
                _names = names.ToArray();
                Available = _counters.Length > 0;
            }
            catch
            {
                _counters = new PerformanceCounter[0];
                _names = new string[0];
                Available = false;
            }
        }

        private static string CleanName(string instance)
        {
            string name = instance.Trim().TrimStart('\\');
            if (name.StartsWith("_TZ.", StringComparison.OrdinalIgnoreCase)) name = name.Substring(4);
            return name.Length == 0 ? instance : name;
        }

        /// <summary>Пары «имя зоны — температура в °C».</summary>
        public IList<KeyValuePair<string, double>> Sample()
        {
            EnsureInitialized();
            var result = new List<KeyValuePair<string, double>>();
            if (_counters.Length == 0) return result;

            for (int i = 0; i < _counters.Length; i++)
            {
                try
                {
                    double kelvin = _counters[i].NextValue();
                    if (kelvin > 1000) kelvin /= 10.0;    // некоторые платы отдают десятые доли кельвина
                    double celsius = kelvin - 273.15;
                    if (celsius > 0 && celsius < 150)
                        result.Add(new KeyValuePair<string, double>(_names[i], celsius));
                }
                catch
                {
                }
            }
            return result;
        }

        public void Dispose()
        {
            foreach (PerformanceCounter counter in _counters)
            {
                try { counter.Dispose(); }
                catch { }
            }
            _counters = new PerformanceCounter[0];
            _names = new string[0];
        }
    }

    /// <summary>
    /// Загрузка видеоядер и занятая видеопамять по счётчикам "GPU Engine" /
    /// "GPU Adapter Memory". Сопоставление с адаптером — по LUID (см. DxgiAdapters).
    ///
    /// Экземпляры счётчиков именуются по процессам, поэтому категория читается
    /// целиком (ReadCategory), а загрузка считается как дельта времени занятости
    /// между двумя чтениями — ровно так же, как это делает монитор
    /// производительности Windows.
    /// </summary>
    internal sealed class GpuSampler : IDisposable
    {
        private static readonly Regex LuidPattern =
            new Regex(@"luid_0x([0-9A-Fa-f]{8})_0x([0-9A-Fa-f]{8})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly object _sync = new object();
        private readonly Dictionary<string, CounterSample> _previous = new Dictionary<string, CounterSample>();
        private PerformanceCounterCategory _engine;
        private PerformanceCounterCategory _memory;
        private bool _initialized;
        private DateTime _retryAfter = DateTime.MinValue;
        private int _samples;

        public bool Available { get; private set; }

        public sealed class GpuReading
        {
            public long Luid;
            public double Load3D;
            public double? DedicatedBytes;
            public double? SharedBytes;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            if (DateTime.UtcNow < _retryAfter) return;
            _initialized = true;

            try
            {
                _engine = new PerformanceCounterCategory("GPU Engine");
                _engine.ReadCategory();      // сразу проверяем, что набор доступен
            }
            catch
            {
                _engine = null;
            }

            try
            {
                _memory = new PerformanceCounterCategory("GPU Adapter Memory");
                _memory.ReadCategory();
            }
            catch
            {
                _memory = null;
            }

            Available = _engine != null || _memory != null;
            if (!Available) _retryAfter = DateTime.UtcNow.AddMinutes(5);
        }

        private static long ParseLuid(string instance)
        {
            Match match = LuidPattern.Match(instance ?? string.Empty);
            if (!match.Success) return -1;
            uint high = uint.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            uint low = uint.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((long)high << 32) | low;
        }

        /// <summary>Загрузка 3D-конвейера одного экземпляра счётчика (в процентах).</summary>
        private double CalculateLoad(string instanceName, CounterSample sample)
        {
            CounterSample previous;
            bool hadPrevious = _previous.TryGetValue(instanceName, out previous);
            _previous[instanceName] = sample;
            if (!hadPrevious) return 0;

            if (sample.TimeStamp <= previous.TimeStamp || sample.SystemFrequency <= 0) return 0;

            double seconds = (sample.TimeStamp - previous.TimeStamp) / (double)sample.SystemFrequency;
            if (seconds <= 0.05) return 0;

            long delta = sample.RawValue - previous.RawValue;
            if (delta <= 0) return 0;

            double value = sample.CounterType == PerformanceCounterType.Timer100Ns
                               ? delta / 10000000.0 / seconds * 100.0
                               : delta / seconds;

            return Math.Max(0, Math.Min(100, value));
        }

        private static GpuReading GetOrAdd(Dictionary<long, GpuReading> readings, long luid)
        {
            GpuReading reading;
            if (!readings.TryGetValue(luid, out reading))
            {
                reading = new GpuReading { Luid = luid };
                readings[luid] = reading;
            }
            return reading;
        }

        /// <summary>Снимок по всем адаптерам: LUID -> загрузка 3D и память.</summary>
        public IDictionary<long, GpuReading> Sample()
        {
            var result = new Dictionary<long, GpuReading>();
            lock (_sync)
            {
                EnsureInitialized();
                var alive = new HashSet<string>(StringComparer.Ordinal);

                if (_engine != null)
                {
                    try
                    {
                        InstanceDataCollectionCollection data = _engine.ReadCategory();
                        if (data.Contains("Utilization Percentage"))
                        {
                            foreach (InstanceData instance in data["Utilization Percentage"].Values)
                            {
                                long luid = ParseLuid(instance.InstanceName);
                                if (luid < 0) continue;

                                alive.Add(instance.InstanceName);
                                GpuReading reading = GetOrAdd(result, luid);

                                // В «загрузку GPU» входит только 3D-конвейер,
                                // остальные движки (Copy/Video) считаются отдельно.
                                if (instance.InstanceName.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                                    reading.Load3D += CalculateLoad(instance.InstanceName, instance.Sample);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (_memory != null)
                {
                    try
                    {
                        InstanceDataCollectionCollection data = _memory.ReadCategory();
                        AddMemory(data, "Dedicated Usage", result, alive, true);
                        AddMemory(data, "Shared Usage", result, alive, false);
                    }
                    catch
                    {
                    }
                }

                foreach (GpuReading reading in result.Values)
                    reading.Load3D = Math.Min(100, reading.Load3D);

                // Экземпляры процессов появляются и исчезают — чистим историю.
                if (++_samples % 30 == 0 && _previous.Count > alive.Count + 128)
                {
                    var stale = new List<string>();
                    foreach (string name in _previous.Keys)
                        if (!alive.Contains(name)) stale.Add(name);
                    foreach (string name in stale) _previous.Remove(name);
                }
            }
            return result;
        }

        private static void AddMemory(InstanceDataCollectionCollection data, string counter,
                                      Dictionary<long, GpuReading> readings, HashSet<string> alive, bool dedicated)
        {
            if (!data.Contains(counter)) return;

            foreach (InstanceData instance in data[counter].Values)
            {
                if (instance.InstanceName.IndexOf("phys_0", StringComparison.OrdinalIgnoreCase) < 0) continue;
                long luid = ParseLuid(instance.InstanceName);
                if (luid < 0) continue;

                alive.Add(instance.InstanceName);
                double bytes = instance.Sample.RawValue;
                if (double.IsNaN(bytes) || double.IsInfinity(bytes) || bytes < 0) continue;

                GpuReading reading = GetOrAdd(readings, luid);
                if (dedicated) reading.DedicatedBytes = bytes;
                else reading.SharedBytes = bytes;
            }
        }

        public void Dispose()
        {
            _engine = null;
            _memory = null;
            _previous.Clear();
        }
    }
}
