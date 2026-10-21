using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace Berlit
{
    /// <summary>
    /// Реальный мониторинг железа.
    ///
    /// Источники данных:
    ///  * LibreHardwareMonitor — температуры ядер и платы, напряжения, вентиляторы,
    ///    мощность (требует прав администратора: библиотека ставит kernel-драйвер);
    ///  * реестр — имя CPU, плата, BIOS, версия ОС, драйвер видеокарты;
    ///  * счётчики производительности — загрузка CPU по ядрам, частота,
    ///    загрузка GPU, температуры ACPI-зон;
    ///  * DXGI — список видеоадаптеров и их LUID (для привязки счётчиков);
    ///  * IOCTL — модель, серийник и тип шины каждого физического диска;
    ///  * WMI — необязательное дополнение (модули памяти, число ядер, сокет).
    ///
    /// Никаких «демо»-значений: если датчик недоступен, строка помечается "n/a".
    /// </summary>
    public sealed class SystemHardwareProvider : IHardwareProvider
    {
        private const string NoData = "n/a";

        private readonly object _sync = new object();
        private bool _initialized;

        private CpuSampler _cpu;
        private ThermalSampler _thermal;
        private GpuSampler _gpu;
        private StorageProbe _storage;
        private WmiProbe _wmi;
        private readonly LibreHardwareSensors _lhm = new LibreHardwareSensors();

        private IList<GpuAdapterInfo> _adapters = new List<GpuAdapterInfo>();
        private DateTime _adapterStamp = DateTime.MinValue;
        private IDictionary<long, GpuSampler.GpuReading> _gpuReadings =
            new Dictionary<long, GpuSampler.GpuReading>();
        private IList<KeyValuePair<string, double>> _zones = new List<KeyValuePair<string, double>>();
        private List<HardwareReadings> _lhmReadings = new List<HardwareReadings>();
        private int _lhmValueCount;
        private double _cpuPackageTemp = double.NaN;

        private WmiProbe.CpuDetails _cpuDetails;
        private IList<WmiProbe.MemoryModule> _modules = new List<WmiProbe.MemoryModule>();
        private DateTime _wmiStamp = DateTime.MinValue;

        private readonly Dictionary<string, Range> _ranges = new Dictionary<string, Range>();
        private readonly List<string> _warnings = new List<string>();

        private int _logicalCpus = 1;
        private double _nominalMhz = 1000;
        private double _ramTotalGb, _ramUsedGb, _ramAvailableGb;
        private double _cpuLoad, _gpuLoad;
        private DateTime _lastSample = DateTime.MinValue;

        private string _status = "Starting...";
        private string _summary = string.Empty;

        private sealed class Range
        {
            public double Min = double.MaxValue;
            public double Max = double.MinValue;

            public void Add(double value)
            {
                if (value < Min) Min = value;
                if (value > Max) Max = value;
            }

            public bool HasValue { get { return Max >= Min; } }
        }

        // ==================================================================
        // Инициализация и опрос датчиков
        // ==================================================================

        private void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_sync)
            {
                if (_initialized) return;

                MachineInfo.EnsureLoaded();
                _logicalCpus = Math.Max(1, Environment.ProcessorCount);
                _nominalMhz = MachineInfo.CpuNominalMhz > 0 ? MachineInfo.CpuNominalMhz : 1000;

                _cpu = new CpuSampler(_nominalMhz);
                _thermal = new ThermalSampler();
                _gpu = new GpuSampler();
                _storage = new StorageProbe();
                _wmi = new WmiProbe();

                RefreshAdapters();
                _initialized = true;
            }
        }

        private void RefreshAdapters()
        {
            _adapterStamp = DateTime.UtcNow;
            try
            {
                _adapters = DxgiAdapters.Enumerate();
            }
            catch
            {
                _adapters = new List<GpuAdapterInfo>();
            }
        }

        /// <summary>Снимает показания со счётчиков не чаще 400 мс (GUI опрашивает 6 групп подряд).</summary>
        private void EnsureSampled()
        {
            EnsureInitialized();
            lock (_sync)
            {
                double age = (DateTime.UtcNow - _lastSample).TotalMilliseconds;
                if (age < 400 && age >= 0) return;
                _lastSample = DateTime.UtcNow;

                try
                {
                    _cpu.Sample();
                    _cpuLoad = _cpu.TotalLoad;
                }
                catch
                {
                }

                if ((DateTime.UtcNow - _adapterStamp).TotalSeconds > 60) RefreshAdapters();

                try
                {
                    _zones = _thermal.Sample();
                }
                catch
                {
                    _zones = new List<KeyValuePair<string, double>>();
                }

                try
                {
                    _gpuReadings = _gpu.Sample();
                    _gpuLoad = 0;
                    foreach (GpuAdapterInfo adapter in _adapters)
                    {
                        GpuSampler.GpuReading reading;
                        if (_gpuReadings.TryGetValue(adapter.Luid, out reading))
                            _gpuLoad = Math.Max(_gpuLoad, reading.Load3D);
                    }
                }
                catch
                {
                    _gpuReadings = new Dictionary<long, GpuSampler.GpuReading>();
                    _gpuLoad = 0;
                }

                NativeMethods.MemoryStatusEx memory;
                if (NativeMethods.TryGetMemory(out memory) && memory.TotalPhys > 0)
                {
                    const double gb = 1024.0 * 1024 * 1024;
                    _ramTotalGb = memory.TotalPhys / gb;
                    _ramAvailableGb = memory.AvailPhys / gb;
                    _ramUsedGb = _ramTotalGb - _ramAvailableGb;
                }

                EnsureWmiData();

                try
                {
                    _lhmReadings = _lhm.Read();
                    UpdateLhmSummary();
                }
                catch
                {
                    _lhmReadings = new List<HardwareReadings>();
                    _lhmValueCount = 0;
                }

                UpdateStatus();
            }
        }

        private void UpdateLhmSummary()
        {
            _lhmValueCount = 0;
            _cpuPackageTemp = double.NaN;

            foreach (HardwareReadings hardware in _lhmReadings)
            {
                foreach (HardwareReadings part in Enumerate(hardware))
                {
                    foreach (SensorReading sensor in part.Sensors)
                    {
                        if (!IsMeasured(sensor)) continue;

                        // Считаем только «главные» датчики: температуры CPU и платы,
                        // их напряжения. GPU-частота и напряжение читаются и без прав,
                        // поэтому по ним нельзя судить о полноте данных.
                        bool mainHardware = part.Type == HardwareType.Cpu ||
                                            part.Type == HardwareType.Motherboard ||
                                            part.Type == HardwareType.SuperIO;

                        if (mainHardware &&
                            (sensor.Type == SensorType.Temperature || sensor.Type == SensorType.Voltage))
                            _lhmValueCount++;

                        if (part.Type == HardwareType.Cpu &&
                            sensor.Type == SensorType.Temperature &&
                            sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase))
                            _cpuPackageTemp = sensor.Value.Value;
                    }
                }
            }
        }

        private void EnsureWmiData()
        {
            double age = (DateTime.UtcNow - _wmiStamp).TotalSeconds;
            if (_cpuDetails != null && age < 60) return;
            if (_cpuDetails == null && age < 3) return;

            _wmiStamp = DateTime.UtcNow;
            _cpuDetails = _wmi.GetCpuDetails();
            _modules = _wmi.GetMemoryModules();
        }

        private void UpdateStatus()
        {
            var parts = new List<string>();
            parts.Add("registry");
            if (_cpu.HasLoad) parts.Add("perf counters");
            if (_adapters.Count > 0) parts.Add("DXGI");
            parts.Add("disk IOCTL");
            if (_wmi.Available) parts.Add("WMI");
            if (_lhm.Available) parts.Add("LibreHardwareMonitor");

            var text = new StringBuilder("Sources: ");
            text.Append(string.Join(", ", parts.ToArray()));

            _warnings.Clear();
            if (_wmi.Checked && !_wmi.Available) _warnings.Add("WMI unavailable");
            if (_zones.Count == 0) _warnings.Add("no ACPI sensors");
            if (_cpu.CoreLoads.Length == 0) _warnings.Add("no per-core counters");
            if (_lhm.Available && _lhmValueCount == 0)
            {
                _warnings.Add(Program.IsAdministrator()
                    ? "sensor driver unavailable (blocked or unsupported)"
                    : "sensors need administrator rights");
            }

            if (_warnings.Count > 0)
            {
                text.Append("  \u00B7  ");
                text.Append(string.Join("; ", _warnings.ToArray()));
            }
            _status = text.ToString();

            var summary = new StringBuilder();
            summary.Append("CPU ");
            summary.Append(_cpu.HasLoad ? HwFormat.Percent(_cpuLoad) : NoData);
            if (!double.IsNaN(_cpuPackageTemp))
                summary.Append("  ").Append(HwFormat.Celsius(_cpuPackageTemp));
            summary.Append("     RAM ");
            summary.Append(_ramTotalGb > 0 ? HwFormat.Gb(_ramUsedGb) + " / " + HwFormat.Gb(_ramTotalGb) : NoData);
            if (_adapters.Count > 0)
            {
                summary.Append("     GPU ");
                summary.Append(_gpuReadings.Count > 0 ? HwFormat.Percent(_gpuLoad) : NoData);
            }
            _summary = summary.ToString();
        }

        // ==================================================================
        // Публичный контракт
        // ==================================================================

        public string Status { get { return _status; } }

        public string Summary { get { return _summary; } }

        public IList<HwNode> GetSystemNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            string title = Environment.MachineName;
            var computer = new HwNode(title, HwNodeKind.Component) { Expanded = true };
            computer.Set(HwCol.Info, Combine(MachineInfo.SystemManufacturer, MachineInfo.SystemProduct));

            string os = Combine(MachineInfo.OsProduct, MachineInfo.OsVersion);
            string build = MachineInfo.OsBuild;
            if (os.Length == 0) os = Environment.OSVersion.VersionString;
            else if (build.Length > 0) os += " (build " + build + ")";

            Sensor(computer, "Operating system").Set(HwCol.Info, os);
            Sensor(computer, "User").Set(HwCol.Info, Environment.UserName);
            Sensor(computer, "Uptime").Set(HwCol.Info, HwFormat.Uptime(NativeMethods.Uptime));
            Sensor(computer, "Architecture").Set(HwCol.Info,
                Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
            if (!string.IsNullOrEmpty(MachineInfo.SystemSerial))
                Sensor(computer, "Serial").Set(HwCol.Info, MachineInfo.SystemSerial, HwLevel.Dim);

            list.Add(computer);
            return list;
        }

        public IList<HwNode> GetCpuNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            string name = string.IsNullOrEmpty(MachineInfo.CpuName) ? "Processor" : MachineInfo.CpuName;
            var cpu = new HwNode(name, HwNodeKind.Component) { Expanded = true };

            int threads = _cpuDetails != null && _cpuDetails.Logical > 0 ? _cpuDetails.Logical : _logicalCpus;
            string summary = ( _cpuDetails != null && _cpuDetails.Cores > 0
                                   ? _cpuDetails.Cores + "C / " + threads + "T"
                                   : threads + " threads") +
                             "  \u00B7  " + HwFormat.Ghz(_nominalMhz) + " base";
            cpu.Set(HwCol.Info, summary, HwLevel.Dim);
            list.Add(cpu);

            HwNode total = Sensor(cpu, "Total load");
            if (_cpu.HasLoad)
                total.Set(HwCol.Load, HwFormat.Percent(_cpuLoad), LoadLevel(_cpuLoad));
            else
                total.Set(HwCol.Load, NoData, HwLevel.Dim);

            HwNode clock = Sensor(cpu, "Effective clock");
            if (_cpu.HasFrequency)
                clock.Set(HwCol.Clock, HwFormat.Ghz(_cpu.FrequencyMhz));
            else
                clock.Set(HwCol.Clock, HwFormat.Ghz(_nominalMhz), HwLevel.Dim)
                     .Set(HwCol.Info, "nominal", HwLevel.Dim);

            if (_cpuDetails != null)
            {
                if (!string.IsNullOrEmpty(_cpuDetails.Socket))
                    Sensor(cpu, "Socket").Set(HwCol.Info, _cpuDetails.Socket, HwLevel.Dim);
                if (_cpuDetails.MaxClockMhz > 0)
                    Sensor(cpu, "Max clock").Set(HwCol.Clock, HwFormat.Ghz(_cpuDetails.MaxClockMhz), HwLevel.Dim);
                if (_cpuDetails.L3 > 0)
                    Sensor(cpu, "L3 cache").Set(HwCol.Info, HwFormat.Bytes(_cpuDetails.L3), HwLevel.Dim);
            }

            for (int i = 0; i < _cpu.CoreLoads.Length; i++)
            {
                double load = _cpu.CoreLoads[i];
                Sensor(cpu, "Core #" + (i + 1).ToString(CultureInfo.InvariantCulture))
                    .Set(HwCol.Load, HwFormat.Percent(load), LoadLevel(load));
            }

            HardwareReadings lhmCpu = FindHardware(HardwareType.Cpu);
            if (lhmCpu != null)
            {
                AddSensorGroup(cpu, "Temperatures", lhmCpu, SensorType.Temperature,
                    delegate (string sensorName)
                    {
                        return sensorName.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase) ||
                               sensorName.Equals("CPU Package", StringComparison.OrdinalIgnoreCase);
                    },
                    CompareCpuTemperatures);

                AddSensorGroup(cpu, "Power", lhmCpu, SensorType.Power, null, null);
                AddSensorGroup(cpu, "Voltage", lhmCpu, SensorType.Voltage, null, null);
            }

            return list;
        }

        public IList<HwNode> GetMemoryNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            var memory = new HwNode("Physical memory", HwNodeKind.Component) { Expanded = true };
            memory.Set(HwCol.Info, _ramTotalGb > 0 ? HwFormat.Gb(_ramTotalGb) + " total" : NoData, HwLevel.Dim);

            double usedPercent = _ramTotalGb > 0 ? _ramUsedGb / _ramTotalGb * 100.0 : 0;
            HwNode used = Sensor(memory, "Memory used");
            if (_ramTotalGb > 0)
                used.Set(HwCol.Load, HwFormat.Percent(usedPercent), LoadLevel(usedPercent))
                    .Set(HwCol.Info, HwFormat.Gb(_ramUsedGb) + " / " + HwFormat.Gb(_ramTotalGb));
            else
                used.Set(HwCol.Load, NoData, HwLevel.Dim);

            if (_ramTotalGb > 0)
                Sensor(memory, "Memory available").Set(HwCol.Info, HwFormat.Gb(_ramAvailableGb));

            foreach (WmiProbe.MemoryModule module in _modules)
            {
                string details = HwFormat.Gb(module.Capacity / (1024.0 * 1024 * 1024));
                if (!string.IsNullOrEmpty(module.TypeName)) details += " " + module.TypeName;
                int speed = module.ConfiguredSpeed > 0 ? module.ConfiguredSpeed : module.Speed;
                if (speed > 0) details += " \u00B7 " + HwFormat.Mhz(speed) + " MHz";
                if (!string.IsNullOrEmpty(module.Manufacturer)) details += " \u00B7 " + module.Manufacturer;

                HwNode row = Sensor(memory, module.Locator);
                row.Set(HwCol.Info, details, HwLevel.Dim);
                if (speed > 0) row.Set(HwCol.Clock, HwFormat.Mhz(speed), HwLevel.Dim);
            }

            list.Add(memory);
            return list;
        }

        public IList<HwNode> GetGpuNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            if (_adapters.Count == 0)
            {
                var unknown = new HwNode("Video adapter", HwNodeKind.Component) { Expanded = true };
                unknown.Set(HwCol.Info, "adapter not detected (DXGI unavailable)", HwLevel.Dim);
                Sensor(unknown, "GPU load").Set(HwCol.Load, NoData, HwLevel.Dim);
                list.Add(unknown);
                return list;
            }

            foreach (GpuAdapterInfo adapter in _adapters)
            {
                var gpu = new HwNode(adapter.Name, HwNodeKind.Component) { Expanded = true };

                string driver = MachineInfo.GetGpuDriverVersion(adapter.Name);
                string pci = string.Format(CultureInfo.InvariantCulture, "{0:X4}:{1:X4}", adapter.VendorId, adapter.DeviceId);
                string info = Combine(adapter.VendorName, "PCI " + pci);
                if (!string.IsNullOrEmpty(driver)) info += "  \u00B7  driver " + driver;
                gpu.Set(HwCol.Info, info, HwLevel.Dim);
                list.Add(gpu);

                GpuSampler.GpuReading reading;
                bool hasReading = _gpuReadings.TryGetValue(adapter.Luid, out reading);

                HwNode load = Sensor(gpu, "GPU load (3D)");
                if (hasReading && _gpu.Available)
                    load.Set(HwCol.Load, HwFormat.Percent(reading.Load3D), LoadLevel(reading.Load3D));
                else
                    load.Set(HwCol.Load, NoData, HwLevel.Dim);

                // У встроенного видео «выделенная» память — это небольшое окно
                // в ОЗУ, поэтому проценты по ней ничего не значат: показываем
                // реально занятую общую память.
                bool integrated = adapter.DedicatedVideoMemory < 512L * 1024 * 1024;

                HwNode memory = Sensor(gpu, "GPU memory");
                if (hasReading && !integrated && reading.DedicatedBytes.HasValue && adapter.DedicatedVideoMemory > 0)
                {
                    double dedicated = reading.DedicatedBytes.Value;
                    double limit = adapter.DedicatedVideoMemory;
                    double percent = limit > 0 ? dedicated / limit * 100.0 : 0;
                    memory.Set(HwCol.Load, HwFormat.Percent(percent), LoadLevel(percent))
                          .Set(HwCol.Info, HwFormat.Bytes((long)dedicated) + " / " + HwFormat.Bytes((long)limit));
                }
                else if (hasReading && reading.SharedBytes.HasValue)
                {
                    memory.Set(HwCol.Info, HwFormat.Bytes((long)reading.SharedBytes.Value) + " shared in RAM", HwLevel.Dim);
                }
                else
                {
                    memory.Set(HwCol.Info, NoData, HwLevel.Dim);
                }

                if (adapter.DedicatedVideoMemory > 0)
                    Sensor(gpu, "Dedicated VRAM").Set(HwCol.Info, HwFormat.Bytes(adapter.DedicatedVideoMemory), HwLevel.Dim);
                if (adapter.SharedSystemMemory > 0)
                    Sensor(gpu, "Shared system memory").Set(HwCol.Info, HwFormat.Bytes(adapter.SharedSystemMemory), HwLevel.Dim);

                HardwareReadings lhmGpu = FindGpu(adapter.Name);
                if (lhmGpu != null) AddGpuSensors(gpu, lhmGpu);
            }

            return list;
        }

        public IList<HwNode> GetStorageNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            IList<StorageProbe.DiskInfo> disks;
            try
            {
                disks = _storage.Read();
            }
            catch
            {
                disks = new List<StorageProbe.DiskInfo>();
            }

            foreach (StorageProbe.DiskInfo disk in disks)
            {
                string title = string.IsNullOrEmpty(disk.Model)
                                   ? (disk.IsPhysical ? "Disk " + disk.Number : "Volume")
                                   : disk.Model;
                var node = new HwNode(title, HwNodeKind.Component) { Expanded = true };

                string info = Combine(disk.Bus,
                    disk.Size > 0 ? HwFormat.Bytes(disk.Size) : string.Empty,
                    string.IsNullOrEmpty(disk.Serial) ? string.Empty : "SN " + disk.Serial);
                node.Set(HwCol.Info, info, HwLevel.Dim);
                list.Add(node);

                if (disk.Volumes.Count == 0)
                {
                    Sensor(node, "Volumes").Set(HwCol.Info, "no mounted volumes", HwLevel.Dim);
                    continue;
                }

                foreach (StorageProbe.VolumeInfo volume in disk.Volumes)
                {
                    long usedBytes = Math.Max(0, volume.Total - volume.Free);
                    double percent = volume.Total > 0 ? usedBytes * 100.0 / volume.Total : 0;

                    string name = volume.Root;
                    if (!string.IsNullOrEmpty(volume.Label)) name += "  " + volume.Label;

                    string details = HwFormat.Bytes(volume.Free) + " free of " + HwFormat.Bytes(volume.Total);
                    if (!string.IsNullOrEmpty(volume.Format)) details += "  (" + volume.Format + ")";

                    Sensor(node, name)
                        .Set(HwCol.Load, HwFormat.Percent(percent), UsageLevel(percent))
                        .Set(HwCol.Info, details);
                }

                AddStorageSensors(node, disk.Model);
            }

            if (list.Count == 0)
            {
                var none = new HwNode("Storage", HwNodeKind.Component) { Expanded = true };
                none.Set(HwCol.Info, "no disks reported", HwLevel.Dim);
                list.Add(none);
            }

            return list;
        }

        public IList<HwNode> GetMainboardNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            string title = string.IsNullOrEmpty(MachineInfo.BoardProduct) ? "Mainboard" : MachineInfo.BoardProduct;
            var board = new HwNode(title, HwNodeKind.Component) { Expanded = true };
            board.Set(HwCol.Info, MachineInfo.BoardManufacturer, HwLevel.Dim);
            list.Add(board);

            Sensor(board, "BIOS").Set(HwCol.Info,
                Combine(MachineInfo.BiosVendor, MachineInfo.BiosVersion, MachineInfo.BiosDate), HwLevel.Dim);

            string system = Combine(MachineInfo.SystemManufacturer, MachineInfo.SystemProduct);
            if (system.Length > 0) Sensor(board, "System").Set(HwCol.Info, system, HwLevel.Dim);
            if (!string.IsNullOrEmpty(MachineInfo.BoardSerial))
                Sensor(board, "Board serial").Set(HwCol.Info, MachineInfo.BoardSerial, HwLevel.Dim);

            HwNode temperatures = null;
            HardwareReadings lhmBoard = FindHardware(HardwareType.Motherboard);
            if (lhmBoard != null)
            {
                var parts = new List<HardwareReadings>(Enumerate(lhmBoard));
                temperatures = AddBoardSensorGroup(board, "Temperatures", parts, SensorType.Temperature);
                AddBoardSensorGroup(board, "Voltages", parts, SensorType.Voltage);
                AddBoardSensorGroup(board, "Fans", parts, SensorType.Fan);
            }

            if (_zones.Count > 0)
            {
                if (temperatures == null)
                    temperatures = board.Add(new HwNode("Temperatures", HwNodeKind.Component) { Expanded = true });

                foreach (KeyValuePair<string, double> zone in _zones)
                {
                    double value = zone.Value;
                    Range range = Track("tz/" + zone.Key, value);

                    HwNode row = Sensor(temperatures, "ACPI zone " + zone.Key);
                    if (range.HasValue)
                        row.Set(HwCol.TempMin, HwFormat.Celsius(range.Min));
                    row.Set(HwCol.Temp, HwFormat.Celsius(value), TemperatureLevel(value));
                    if (range.HasValue)
                        row.Set(HwCol.TempMax, HwFormat.Celsius(range.Max));
                }
            }
            else if (temperatures == null && !_lhm.Available)
            {
                Sensor(board, "Temperatures").Set(HwCol.Info, "not exposed by firmware", HwLevel.Dim);
            }

            return list;
        }

        // ==================================================================
        // Датчики LibreHardwareMonitor
        // ==================================================================

        private static IEnumerable<HardwareReadings> Enumerate(HardwareReadings hardware)
        {
            if (hardware == null) yield break;

            yield return hardware;
            foreach (HardwareReadings sub in hardware.SubHardware)
            {
                foreach (HardwareReadings item in Enumerate(sub))
                    yield return item;
            }
        }

        private HardwareReadings FindHardware(HardwareType type)
        {
            foreach (HardwareReadings hardware in _lhmReadings)
                if (hardware.Type == type) return hardware;
            return null;
        }

        private HardwareReadings FindGpu(string adapterName)
        {
            foreach (HardwareReadings hardware in _lhmReadings)
            {
                switch (hardware.Type)
                {
                    case HardwareType.GpuIntel:
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                        break;
                    default:
                        continue;
                }

                if (NamesMatch(hardware.Name, adapterName)) return hardware;
            }
            return null;
        }

        private HardwareReadings FindStorage(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;

            foreach (HardwareReadings hardware in _lhmReadings)
            {
                if (hardware.Type == HardwareType.Storage && NamesMatch(hardware.Name, model))
                    return hardware;
            }
            return null;
        }

        private static bool NamesMatch(string left, string right)
        {
            string a = NormalizeName(left);
            string b = NormalizeName(right);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;
            if (a.Length >= 6 && b.IndexOf(a, StringComparison.Ordinal) >= 0) return true;
            if (b.Length >= 6 && a.IndexOf(b, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string NormalizeName(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            return builder.ToString();
        }

        /// <summary>Нулевые и пустые показания LHM — это «датчик не прочитан», а не 0 °C / 0 В.</summary>
        private static bool IsMeasured(SensorReading sensor)
        {
            if (sensor == null || !sensor.Value.HasValue) return false;

            double value = sensor.Value.Value;
            return value > 0.01 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void AddSensorGroup(HwNode parent, string title, HardwareReadings hardware, SensorType type,
                                    Func<string, bool> include, Comparison<SensorReading> order)
        {
            var selected = new List<SensorReading>();
            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != type || !IsMeasured(sensor)) continue;
                if (include != null && !include(sensor.Name)) continue;
                selected.Add(sensor);
            }

            if (selected.Count == 0) return;
            if (order != null) selected.Sort(order);

            HwNode group = parent.Add(new HwNode(title, HwNodeKind.Component) { Expanded = true });
            foreach (SensorReading sensor in selected)
                ApplySensorValue(Sensor(group, sensor.Name), type, sensor.Value.Value);
        }

        private HwNode AddBoardSensorGroup(HwNode board, string title, List<HardwareReadings> parts, SensorType type)
        {
            var selected = new List<SensorReading>();
            foreach (HardwareReadings part in parts)
            {
                foreach (SensorReading sensor in part.Sensors)
                {
                    if (sensor.Type == type && IsMeasured(sensor)) selected.Add(sensor);
                }
            }

            if (selected.Count == 0) return null;

            selected.Sort(delegate (SensorReading a, SensorReading b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            HwNode group = board.Add(new HwNode(title, HwNodeKind.Component) { Expanded = true });
            foreach (SensorReading sensor in selected)
                ApplySensorValue(Sensor(group, sensor.Name), type, sensor.Value.Value);
            return group;
        }

        private void AddGpuSensors(HwNode gpu, HardwareReadings hardware)
        {
            AddGpuRow(gpu, hardware, "Core temperature", SensorType.Temperature, "GPU Core");
            AddGpuRow(gpu, hardware, "Hot spot", SensorType.Temperature, "GPU Hot Spot");
            AddGpuRow(gpu, hardware, "Memory temperature", SensorType.Temperature, "GPU Memory");
            AddGpuRow(gpu, hardware, "Core clock", SensorType.Clock, "GPU Core");
            AddGpuRow(gpu, hardware, "Fan", SensorType.Fan, null);
            AddGpuRow(gpu, hardware, "Power", SensorType.Power, null);
            AddGpuRow(gpu, hardware, "Voltage", SensorType.Voltage, null);
        }

        private void AddGpuRow(HwNode gpu, HardwareReadings hardware, string title, SensorType type, string sensorName)
        {
            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != type || !IsMeasured(sensor)) continue;
                if (sensorName != null && !sensor.Name.Equals(sensorName, StringComparison.OrdinalIgnoreCase)) continue;

                ApplySensorValue(Sensor(gpu, title), type, sensor.Value.Value);
                return;
            }
        }

        private void AddStorageSensors(HwNode disk, string model)
        {
            HardwareReadings hardware = FindStorage(model);
            if (hardware == null) return;

            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != SensorType.Temperature || !IsMeasured(sensor)) continue;

                ApplySensorValue(Sensor(disk, "Temperature"), SensorType.Temperature, sensor.Value.Value);
                return;
            }
        }

        private void ApplySensorValue(HwNode row, SensorType type, double value)
        {
            switch (type)
            {
                case SensorType.Temperature:
                    Range range = Track("lhm/" + row.Path, value);
                    if (range.HasValue) row.Set(HwCol.TempMin, HwFormat.Celsius(range.Min));
                    row.Set(HwCol.Temp, HwFormat.Celsius(value), TemperatureLevel(value));
                    if (range.HasValue) row.Set(HwCol.TempMax, HwFormat.Celsius(range.Max));
                    break;

                case SensorType.Voltage:
                    row.Set(HwCol.Voltage, HwFormat.Volt(value));
                    break;

                case SensorType.Power:
                    row.Set(HwCol.Power, HwFormat.Watt(value));
                    break;

                case SensorType.Clock:
                    row.Set(HwCol.Clock, HwFormat.Ghz(value));
                    break;

                case SensorType.Fan:
                    row.Set(HwCol.FanRpm, HwFormat.Rpm(value));
                    break;

                case SensorType.Control:
                    row.Set(HwCol.Info, HwFormat.Percent(value), HwLevel.Dim);
                    break;
            }
        }

        private static int CompareCpuTemperatures(SensorReading left, SensorReading right)
        {
            bool leftPackage = left.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase);
            bool rightPackage = right.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase);
            if (leftPackage != rightPackage) return leftPackage ? -1 : 1;

            int leftIndex = CoreIndex(left.Name);
            int rightIndex = CoreIndex(right.Name);
            if (leftIndex != rightIndex) return leftIndex.CompareTo(rightIndex);

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CoreIndex(string name)
        {
            int hash = name.IndexOf('#');
            if (hash < 0 || hash + 1 >= name.Length) return int.MaxValue;

            int index = 0;
            for (int i = hash + 1; i < name.Length && char.IsDigit(name[i]); i++)
                index = index * 10 + (name[i] - '0');
            return index;
        }

        // ==================================================================
        // Вспомогательное
        // ==================================================================

        private Range Track(string key, double value)
        {
            lock (_sync)
            {
                Range range;
                if (!_ranges.TryGetValue(key, out range))
                {
                    range = new Range();
                    _ranges[key] = range;
                }
                range.Add(value);
                return range;
            }
        }

        private static HwNode Sensor(HwNode parent, string text)
        {
            return parent.Add(new HwNode(text, HwNodeKind.Sensor));
        }

        private static HwLevel LoadLevel(double percent)
        {
            if (percent >= 90) return HwLevel.Critical;
            if (percent >= 75) return HwLevel.Warn;
            return HwLevel.Normal;
        }

        private static HwLevel UsageLevel(double percent)
        {
            if (percent >= 95) return HwLevel.Critical;
            if (percent >= 85) return HwLevel.Warn;
            return HwLevel.Normal;
        }

        private static HwLevel TemperatureLevel(double celsius)
        {
            if (celsius >= 95) return HwLevel.Critical;
            if (celsius >= 80) return HwLevel.Warn;
            return HwLevel.Normal;
        }

        private static string Combine(params string[] parts)
        {
            var builder = new StringBuilder();
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (builder.Length > 0) builder.Append("  \u00B7  ");
                builder.Append(part.Trim());
            }
            return builder.ToString();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_cpu != null) _cpu.Dispose();
                if (_thermal != null) _thermal.Dispose();
                if (_gpu != null) _gpu.Dispose();
                try { _lhm.Dispose(); }
                catch { }
                _cpu = null;
                _thermal = null;
                _gpu = null;
                _initialized = false;
            }
        }
    }
}
