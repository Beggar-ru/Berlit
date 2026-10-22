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
        private int _cpuTempPriority;

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
        private HudSnapshot _hud = new HudSnapshot();

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
            _cpuTempPriority = -1;

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

                        if (part.Type == HardwareType.Cpu && sensor.Type == SensorType.Temperature)
                        {
                            // Intel отдаёт «CPU Package», AMD — «Core (Tctl/Tdie)».
                            // Берём самый приоритетный датчик, а не фиксированное имя.
                            int priority = CpuTempPriority(sensor.Name);
                            if (priority > _cpuTempPriority)
                            {
                                _cpuTempPriority = priority;
                                _cpuPackageTemp = sensor.Value.Value;
                            }
                        }
                    }
                }
            }

            double? gpuTemp = null;
            foreach (HardwareReadings hardware in _lhmReadings)
            {
                if (hardware.Type != HardwareType.GpuIntel &&
                    hardware.Type != HardwareType.GpuNvidia &&
                    hardware.Type != HardwareType.GpuAmd) continue;

                SensorReading core = FindSensor(hardware, SensorType.Temperature, "GPU Core");
                if (!IsMeasured(core)) continue;

                gpuTemp = core.Value.Value;
                break;
            }

            _hud = new HudSnapshot
            {
                CpuLoad = _cpuLoad,
                CpuTemp = double.IsNaN(_cpuPackageTemp) ? (double?)null : _cpuPackageTemp,
                HasGpu = _adapters.Count > 0,
                GpuLoad = _adapters.Count > 0 ? (double?)_gpuLoad : null,
                GpuTemp = gpuTemp,
                RamPercent = _ramTotalGb > 0 ? _ramUsedGb / _ramTotalGb * 100.0 : 0
            };
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
                    ? "sensor driver blocked (WinRing0); PawnIO needed"
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

        public HudSnapshot GetHudSnapshot()
        {
            return _hud;
        }

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
            list.Add(cpu);

            HardwareReadings lhmCpu = FindHardware(HardwareType.Cpu);

            int threads = _cpuDetails != null && _cpuDetails.Logical > 0 ? _cpuDetails.Logical : _logicalCpus;
            string summary = (_cpuDetails != null && _cpuDetails.Cores > 0
                                  ? _cpuDetails.Cores + "C / " + threads + "T"
                                  : threads + " threads") +
                             "  \u00B7  " + HwFormat.Ghz(_nominalMhz) + " base";
            cpu.Set(HwCol.Info, summary, HwLevel.Dim);

            // Всё, что относится к процессору целиком, — в его собственной строке.
            if (_cpu.HasLoad)
                cpu.Set(HwCol.Load, HwFormat.Percent(_cpuLoad), LoadLevel(_cpuLoad));
            else
                cpu.Set(HwCol.Load, NoData, HwLevel.Dim);

            if (_cpu.HasFrequency)
                cpu.Set(HwCol.Clock, HwFormat.Ghz(_cpu.FrequencyMhz));
            else
                cpu.Set(HwCol.Clock, HwFormat.Ghz(_nominalMhz), HwLevel.Dim);

            SensorReading packageTemp = FindCpuTemperature(lhmCpu);
            if (IsMeasured(packageTemp))
                ApplyTemperature(cpu, packageTemp.Value.Value, "cpu.package");

            SensorReading packagePower = FindSensor(lhmCpu, SensorType.Power, "CPU Package");
            if (IsMeasured(packagePower))
                cpu.Set(HwCol.Power, HwFormat.Watt(packagePower.Value.Value));

            SensorReading cpuVoltage = FindAnySensor(lhmCpu, SensorType.Voltage);
            if (IsMeasured(cpuVoltage))
                cpu.Set(HwCol.Voltage, HwFormat.Volt(cpuVoltage.Value.Value));

            var coreTemps = new Dictionary<int, SensorReading>();
            var coreClocks = new Dictionary<int, SensorReading>();
            var coreVolts = new Dictionary<int, SensorReading>();
            CollectCoreSensors(lhmCpu, SensorType.Temperature, coreTemps);
            CollectCoreSensors(lhmCpu, SensorType.Clock, coreClocks);
            CollectCoreSensors(lhmCpu, SensorType.Voltage, coreVolts);

            int coreCount = Math.Max(coreTemps.Count, Math.Max(coreClocks.Count, coreVolts.Count));
            if (coreCount == 0 && _cpuDetails != null && _cpuDetails.Cores > 0)
                coreCount = _cpuDetails.Cores;
            if (coreCount == 0)
                coreCount = _cpu.CoreLoads.Length;

            // Одна строка на ядро: загрузка, температура (min/сейчас/max), частота, напряжение.
            for (int i = 0; i < coreCount; i++)
            {
                HwNode row = Sensor(cpu, "Core #" + (i + 1).ToString(CultureInfo.InvariantCulture));

                double? load = CoreLoadFor(i, coreCount, _cpu.CoreLoads);
                if (load.HasValue)
                    row.Set(HwCol.Load, HwFormat.Percent(load.Value), LoadLevel(load.Value));

                SensorReading temperature;
                if (coreTemps.TryGetValue(i + 1, out temperature))
                    ApplyTemperature(row, temperature.Value.Value,
                        "cpu.core" + (i + 1).ToString(CultureInfo.InvariantCulture));

                SensorReading clock;
                if (coreClocks.TryGetValue(i + 1, out clock))
                    row.Set(HwCol.Clock, HwFormat.Ghz(clock.Value.Value));

                SensorReading voltage;
                if (coreVolts.TryGetValue(i + 1, out voltage))
                    row.Set(HwCol.Voltage, HwFormat.Volt(voltage.Value.Value));
            }

            // Socket, Max clock и L3 cache намеренно не выводятся: это дублирует
            // SYSTEM INFO и занимает место в живой таблице.

            return list;
        }

        public IList<HwNode> GetMemoryNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            var memory = new HwNode("Physical memory", HwNodeKind.Component) { Expanded = true };
            list.Add(memory);

            if (_ramTotalGb > 0)
            {
                double usedPercent = _ramUsedGb / _ramTotalGb * 100.0;
                memory.Set(HwCol.Load, HwFormat.Percent(usedPercent), LoadLevel(usedPercent))
                      .Set(HwCol.Info,
                          HwFormat.Gb(_ramUsedGb) + " / " + HwFormat.Gb(_ramTotalGb) +
                          "  \u00B7  " + HwFormat.Gb(_ramAvailableGb) + " free",
                          HwLevel.Dim);
            }
            else
            {
                memory.Set(HwCol.Load, NoData, HwLevel.Dim);
            }

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

            return list;
        }

        public IList<HwNode> GetGpuNodes()
        {
            EnsureSampled();
            var list = new List<HwNode>();

            if (_adapters.Count == 0)
            {
                var unknown = new HwNode("Video adapter", HwNodeKind.Component) { Expanded = true };
                unknown.Set(HwCol.Load, NoData, HwLevel.Dim)
                       .Set(HwCol.Info, "adapter not detected (DXGI unavailable)", HwLevel.Dim);
                list.Add(unknown);
                return list;
            }

            foreach (GpuAdapterInfo adapter in _adapters)
            {
                var gpu = new HwNode(adapter.Name, HwNodeKind.Component) { Expanded = true };
                list.Add(gpu);

                GpuSampler.GpuReading reading;
                bool hasReading = _gpuReadings.TryGetValue(adapter.Luid, out reading);
                HardwareReadings lhmGpu = FindGpu(adapter.Name);

                // Загрузка 3D — в строке самой видеокарты.
                if (hasReading && _gpu.Available)
                    gpu.Set(HwCol.Load, HwFormat.Percent(reading.Load3D), LoadLevel(reading.Load3D));
                else
                    gpu.Set(HwCol.Load, NoData, HwLevel.Dim);

                SensorReading coreTemp = FindSensor(lhmGpu, SensorType.Temperature, "GPU Core");
                if (IsMeasured(coreTemp))
                    ApplyTemperature(gpu, coreTemp.Value.Value,
                        "gpu." + adapter.Luid.ToString(CultureInfo.InvariantCulture));

                // Hot spot — вторая точка нагрева; держим в описании, чтобы не плодить строки.
                string hotSpot = string.Empty;
                SensorReading hotSpotSensor = FindSensor(lhmGpu, SensorType.Temperature, "GPU Hot Spot");
                if (IsMeasured(hotSpotSensor))
                    hotSpot = "hot spot " + HwFormat.Celsius(hotSpotSensor.Value.Value);

                SensorReading fan = FindAnySensor(lhmGpu, SensorType.Fan);
                if (IsMeasured(fan)) gpu.Set(HwCol.FanRpm, HwFormat.Rpm(fan.Value.Value));

                SensorReading clock = FindSensor(lhmGpu, SensorType.Clock, "GPU Core");
                if (IsMeasured(clock)) gpu.Set(HwCol.Clock, HwFormat.Ghz(clock.Value.Value));

                SensorReading memClock = FindSensorContains(lhmGpu, SensorType.Clock, "memory");
                if (IsMeasured(memClock)) gpu.Set(HwCol.MemClock, HwFormat.Ghz(memClock.Value.Value));

                SensorReading voltage = FindSensor(lhmGpu, SensorType.Voltage, "GPU Core");
                if (IsMeasured(voltage)) gpu.Set(HwCol.Voltage, HwFormat.Volt(voltage.Value.Value));

                SensorReading power = FindAnySensor(lhmGpu, SensorType.Power);
                if (IsMeasured(power)) gpu.Set(HwCol.Power, HwFormat.Watt(power.Value.Value));

                string driver = MachineInfo.GetGpuDriverVersion(adapter.Name);
                string pci = string.Format(CultureInfo.InvariantCulture, "{0:X4}:{1:X4}",
                    adapter.VendorId, adapter.DeviceId);
                string vram = adapter.DedicatedVideoMemory > 0
                                  ? HwFormat.Bytes(adapter.DedicatedVideoMemory) + " VRAM"
                                  : string.Empty;

                gpu.Set(HwCol.Info,
                    Combine(adapter.VendorName, "PCI " + pci,
                        driver.Length > 0 ? "driver " + driver : string.Empty, vram, hotSpot),
                    HwLevel.Dim);

                // Занятая видеопамять — отдельная сущность со своим процентом.
                HwNode memory = Sensor(gpu, "GPU memory");
                bool integrated = adapter.DedicatedVideoMemory < 512L * 1024 * 1024;
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
                AddStorageTemperature(node, disk);
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

        /// <summary>Датчик с точным именем (без учёта регистра) и измеренным значением.</summary>
        /// <summary>Приоритет датчика температуры CPU: Intel и AMD называют их по-разному.</summary>
        private static int CpuTempPriority(string name)
        {
            if (name.IndexOf("Tctl/Tdie", StringComparison.OrdinalIgnoreCase) >= 0) return 100;
            if (name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0) return 95;
            if (name.IndexOf("Tdie", StringComparison.OrdinalIgnoreCase) >= 0) return 90;
            if (name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase)) return 85;
            if (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0) return 80;
            if (name.IndexOf("Core Average", StringComparison.OrdinalIgnoreCase) >= 0) return 75;
            if (name.IndexOf("CPU Cores", StringComparison.OrdinalIgnoreCase) >= 0) return 72;
            if (name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0) return 70;
            if (name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
            return 10;
        }

        private static SensorReading FindCpuTemperature(HardwareReadings hardware)
        {
            if (hardware == null) return null;

            SensorReading best = null;
            int bestPriority = -1;
            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != SensorType.Temperature || !IsMeasured(sensor) || sensor.Value.Value <= 0) continue;

                int priority = CpuTempPriority(sensor.Name);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = sensor;
                }
            }
            return best;
        }

        private static SensorReading FindSensorContains(HardwareReadings hardware, SensorType type, string part)
        {
            if (hardware == null) return null;

            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != type || !IsMeasured(sensor)) continue;
                if (sensor.Name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return sensor;
            }
            return null;
        }

        private static SensorReading FindSensor(HardwareReadings hardware, SensorType type, string name)
        {
            if (hardware == null) return null;

            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != type || !IsMeasured(sensor)) continue;
                if (sensor.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return sensor;
            }
            return null;
        }

        private static SensorReading FindAnySensor(HardwareReadings hardware, SensorType type)
        {
            if (hardware == null) return null;

            foreach (SensorReading sensor in hardware.Sensors)
                if (sensor.Type == type && IsMeasured(sensor)) return sensor;
            return null;
        }

        /// <summary>Раскладывает датчики ядер («CPU Core #N») по номеру ядра.</summary>
        private static void CollectCoreSensors(HardwareReadings hardware, SensorType type,
                                               Dictionary<int, SensorReading> target)
        {
            if (hardware == null) return;

            foreach (SensorReading sensor in hardware.Sensors)
            {
                if (sensor.Type != type || !IsMeasured(sensor)) continue;
                if (sensor.Name.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                int core = ParseCoreNumber(sensor.Name);
                if (core <= 0 || target.ContainsKey(core)) continue;
                target[core] = sensor;
            }
        }

        private static int ParseCoreNumber(string name)
        {
            // Intel: «CPU Core #N», AMD: «Core #N». GPU и потоки не трогаем.
            if (name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0) return -1;
            if (name.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) >= 0) return -1;
            if (name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) < 0) return -1;

            int hash = name.IndexOf('#');
            if (hash < 0) return -1;

            int value = 0;
            bool any = false;
            for (int i = hash + 1; i < name.Length && char.IsDigit(name[i]); i++)
            {
                value = value * 10 + (name[i] - '0');
                any = true;
            }
            return any ? value : -1;
        }

        /// <summary>
        /// Загрузка физического ядра. Счётчики Windows дают загрузку по логическим
        /// потокам, поэтому при 2N потоках на N ядер берём среднее пары.
        /// </summary>
        private static double? CoreLoadFor(int index, int cores, double[] loads)
        {
            if (loads == null || loads.Length == 0) return null;
            if (loads.Length == cores) return loads[index];
            if (loads.Length == cores * 2) return (loads[index] + loads[index + cores]) / 2.0;
            if (index < loads.Length) return loads[index];
            return null;
        }

        /// <summary>Температура с накоплением min/max в той же строке, где и остальные параметры.</summary>
        private void ApplyTemperature(HwNode row, double value, string key)
        {
            Range range = Track(key, value);
            if (range.HasValue) row.Set(HwCol.TempMin, HwFormat.Celsius(range.Min));
            row.Set(HwCol.Temp, HwFormat.Celsius(value), TemperatureLevel(value));
            if (range.HasValue) row.Set(HwCol.TempMax, HwFormat.Celsius(range.Max));
        }

        private void AddStorageTemperature(HwNode disk, StorageProbe.DiskInfo info)
        {
            HardwareReadings hardware = FindStorage(info.Model);
            if (hardware == null) return;

            SensorReading sensor = FindAnySensor(hardware, SensorType.Temperature);
            if (!IsMeasured(sensor)) return;

            string key = info.Number >= 0
                             ? info.Number.ToString(CultureInfo.InvariantCulture)
                             : info.Model;
            ApplyTemperature(disk, sensor.Value.Value, "disk." + key);
        }

        private void ApplySensorValue(HwNode row, SensorType type, double value)
        {
            switch (type)
            {
                case SensorType.Temperature:
                    ApplyTemperature(row, value, "lhm/" + row.Path);
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
