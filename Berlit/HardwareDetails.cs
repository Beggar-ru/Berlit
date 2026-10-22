using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Berlit
{
    /// <summary>Строка «параметр — значение» на вкладке подробностей.</summary>
    public sealed class InfoRow
    {
        public InfoRow(string label, string value, HwLevel level = HwLevel.Normal)
        {
            Label = label;
            Value = value ?? string.Empty;
            Level = level;
        }

        public string Label { get; private set; }
        public string Value { get; private set; }
        public HwLevel Level { get; private set; }
    }

    /// <summary>Блок подробностей: заголовок и набор строк. Может сворачиваться.</summary>
    public sealed class InfoSection
    {
        public InfoSection(string title, bool collapsible = false)
        {
            Title = title;
            Collapsible = collapsible;
        }

        public string Title { get; private set; }
        public bool Collapsible { get; private set; }
        public bool Collapsed { get; set; }
        public List<InfoRow> Rows { get; private set; } = new List<InfoRow>();

        public InfoSection Add(string label, string value, HwLevel level = HwLevel.Normal)
        {
            Rows.Add(new InfoRow(label, value, level));
            return this;
        }
    }

    /// <summary>
    /// Сбор статических сведений о компонентах: SMBIOS (прошивка), реестр,
    /// DXGI, IOCTL, CPUID-подобные данные из системных API. Без WMI и без
    /// собственного драйвера.
    /// </summary>
    internal static class HardwareDetails
    {
        // ------------------------------------------------------------------
        // CPU
        // ------------------------------------------------------------------

        public static IList<InfoSection> GetCpuSections()
        {
            var sections = new List<InfoSection>();

            MachineInfo.EnsureLoaded();
            SmbiosStructure processor = SmbiosReader.First(4);

            string name = string.IsNullOrEmpty(MachineInfo.CpuName)
                              ? (processor != null ? processor.Text(0x10) : "Processor")
                              : MachineInfo.CpuName;
            string identifier = string.IsNullOrEmpty(MachineInfo.CpuIdentifier)
                                    ? (processor != null ? processor.Text(0x10) : string.Empty)
                                    : MachineInfo.CpuIdentifier;

            int family, model, stepping;
            ParseIdentifier(identifier, out family, out model, out stepping);

            string vendor = VendorName(MachineInfo.CpuVendor, identifier);
            string socket = processor != null ? processor.Text(0x04) : string.Empty;
            if (string.IsNullOrEmpty(socket) || socket.IndexOf("O.E.M.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                !LooksLikeSocket(socket))
            {
                string mapped = SocketFor(vendor, family, model);
                if (mapped.Length > 0) socket = mapped;
            }
            string codename = Codename(vendor, family, model);

            var main = new InfoSection("Processor");
            main.Add("Name", name);
            if (!string.IsNullOrEmpty(codename)) main.Add("Codename", codename);
            if (!string.IsNullOrEmpty(socket)) main.Add("Socket", socket);
            string technology = Technology(vendor, family, model);
            if (!string.IsNullOrEmpty(technology)) main.Add("Technology", technology);
            if (!string.IsNullOrEmpty(vendor)) main.Add("Vendor", vendor);
            main.Add("Family", FormatFamilyModel(family, "Family"));
            main.Add("Model", FormatFamilyModel(model, "Model"));
            main.Add("Stepping", stepping.ToString(CultureInfo.InvariantCulture) + " (" +
                                 stepping.ToString("X", CultureInfo.InvariantCulture) + "h)");

            string microcode = MicrocodeRevision();
            if (!string.IsNullOrEmpty(microcode)) main.Add("Microcode", microcode);

            int cores, threads;
            GetCoreCounts(processor, out cores, out threads);
            main.Add("Cores", cores.ToString(CultureInfo.InvariantCulture));
            main.Add("Threads", threads.ToString(CultureInfo.InvariantCulture));
            sections.Add(main);

            // Частоты
            var clocks = new InfoSection("Clocks");
            double baseMhz = processor != null ? processor.Word(0x16) : 0;
            if (baseMhz <= 0) baseMhz = MachineInfo.CpuNominalMhz;
            double maxMhz = processor != null ? processor.Word(0x14) : 0;
            if (baseMhz > 0) clocks.Add("Core speed", HwFormat.Ghz(baseMhz));
            if (maxMhz > 0) clocks.Add("Max speed", HwFormat.Ghz(maxMhz));
            if (processor != null && processor.Word(0x12) > 0)
                clocks.Add("Bus speed", HwFormat.Mhz(processor.Word(0x12)) + " MHz");
            if (clocks.Rows.Count > 0) sections.Add(clocks);

            // Кэши
            var caches = BuildCaches(processor);
            if (caches != null && caches.Rows.Count > 0) sections.Add(caches);

            // Инструкции
            string instructions = BuildInstructions();
            if (instructions.Length > 0)
            {
                var features = new InfoSection("Instructions");
                features.Add("Supported", instructions);
                sections.Add(features);
            }

            return sections;
        }

        private static string FormatFamilyModel(int value, string label)
        {
            return value.ToString(CultureInfo.InvariantCulture) + " (" +
                   value.ToString("X", CultureInfo.InvariantCulture) + "h)";
        }

        private static void ParseIdentifier(string identifier, out int family, out int model, out int stepping)
        {
            family = 0;
            model = 0;
            stepping = 0;
            if (string.IsNullOrEmpty(identifier)) return;

            Match match = Regex.Match(identifier, @"Family\s+(\d+)\s+Model\s+(\d+)\s+Stepping\s+(\d+)");
            if (!match.Success) return;

            int.TryParse(match.Groups[1].Value, out family);
            int.TryParse(match.Groups[2].Value, out model);
            int.TryParse(match.Groups[3].Value, out stepping);
        }

        private static string VendorName(string vendorIdentifier, string identifier)
        {
            string text = (vendorIdentifier ?? string.Empty) + " " + (identifier ?? string.Empty);
            if (text.IndexOf("GenuineIntel", StringComparison.OrdinalIgnoreCase) >= 0) return "Intel";
            if (text.IndexOf("AuthenticAMD", StringComparison.OrdinalIgnoreCase) >= 0) return "AMD";
            if (!string.IsNullOrEmpty(vendorIdentifier)) return vendorIdentifier;
            return string.Empty;
        }

        private static string Codename(string vendor, int family, int model)
        {
            if (vendor == "Intel" && family == 6)
            {
                switch (model)
                {
                    case 0x1A: case 0x1E: case 0x1F: return "Nehalem / Bloomfield / Lynnfield";
                    case 0x25: case 0x2C: return "Westmere";
                    case 0x2A: case 0x2D: return "Sandy Bridge";
                    case 0x3A: case 0x3E: return "Ivy Bridge";
                    case 0x3C: case 0x3F: case 0x45: case 0x46: return "Haswell";
                    case 0x3D: case 0x47: case 0x4F: case 0x56: return "Broadwell";
                    case 0x4E: case 0x5E: return "Skylake";
                    case 0x55: return "Skylake-X / Cascade Lake";
                    case 0x8E: case 0x9E: return "Kaby Lake / Coffee Lake";
                    case 0x66: return "Cannon Lake";
                    case 0x7D: case 0x7E: return "Ice Lake";
                    case 0x8C: case 0x8D: return "Tiger Lake";
                    case 0xA5: case 0xA6: return "Comet Lake";
                    case 0xA7: return "Rocket Lake";
                    case 0x97: case 0x9A: return "Alder Lake";
                    case 0xB7: case 0xBA: return "Raptor Lake";
                    case 0xAA: return "Meteor Lake";
                    case 0xAC: return "Lunar Lake";
                    case 0xBF: return "Emerald Rapids";
                    case 0xCF: return "Granite Rapids";
                }
            }
            if (vendor == "AMD")
            {
                if (family == 0x17) return "Zen / Zen+ / Zen 2";
                if (family == 0x19) return "Zen 3 / Zen 4";
                if (family == 0x1A) return "Zen 5";
            }
            return string.Empty;
        }

        private static string Technology(string vendor, int family, int model)
        {
            if (vendor == "Intel" && family == 6)
            {
                switch (model)
                {
                    case 0x97: case 0x9A: case 0xB7: case 0xBA: return "Intel 7 (10 nm)";
                    case 0xAA: return "Intel 4 (7 nm)";
                    case 0xAC: return "Intel 18A";
                    case 0x7D: case 0x7E: case 0x8C: case 0x8D: return "10 nm";
                    case 0xA7: case 0xA5: case 0xA6:
                    case 0x8E: case 0x9E: case 0x4E: case 0x5E: case 0x55: return "14 nm";
                    case 0x3C: case 0x3F: case 0x45: case 0x46: case 0x3D: case 0x47: case 0x4F: case 0x56: return "22 nm / 14 nm";
                    case 0x3A: case 0x3E: return "22 nm";
                    case 0x2A: case 0x2D: return "32 nm";
                }
            }
            if (vendor == "AMD")
            {
                if (family == 0x17) return "14 nm / 12 nm";
                if (family == 0x19) return "7 nm / 5 nm";
                if (family == 0x1A) return "4 nm";
            }
            return string.Empty;
        }

        private static string MicrocodeRevision()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key == null) return string.Empty;
                    byte[] revision = key.GetValue("Update Revision") as byte[];
                    if (revision == null || revision.Length == 0) return string.Empty;

                    // На Intel ревизия микрокода лежит вторым DWORD (первые 4 байта — резерв).
                    uint value = revision.Length >= 8
                                     ? BitConverter.ToUInt32(revision, 4)
                                     : BitConverter.ToUInt32(revision, 0);
                    return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void GetCoreCounts(SmbiosStructure processor, out int cores, out int threads)
        {
            cores = 0;
            threads = 0;

            if (processor != null)
            {
                cores = processor.Byte(0x23);
                threads = processor.Word(0x25);
                if (processor.Data.Length > 0x30 && processor.Word(0x2B) > 0)
                    cores = processor.Word(0x2B);
                if (processor.Data.Length > 0x31 && processor.Word(0x2F) > 0)
                    threads = processor.Word(0x2F);
            }

            // SMBIOS на части плат пишет в Core Count число потоков, поэтому
            // физические ядра считаем по топологии Windows.
            int physical = CountPhysicalCores();
            if (physical > 0) cores = physical;

            if (cores <= 0) cores = Environment.ProcessorCount;
            if (threads <= 0 || threads > 1024 || threads < cores)
                threads = Environment.ProcessorCount;
            if (threads < cores) threads = cores;
        }

        private static bool LooksLikeSocket(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("LGA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("AM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("BGA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("PGA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SocketFor(string vendor, int family, int model)
        {
            if (vendor == "Intel" && family == 6)
            {
                switch (model)
                {
                    case 0x97: case 0x9A: case 0xB7: case 0xBA: return "LGA1700";
                    case 0xA7: case 0xA5: case 0xA6: return "LGA1200";
                    case 0x8E: case 0x9E: case 0x4E: case 0x5E: return "LGA1151";
                }
            }
            if (vendor == "AMD")
            {
                if (family == 0x17 || family == 0x19) return "AM4";
                if (family == 0x1A) return "AM5";
            }
            return string.Empty;
        }

        private static int CountPhysicalCores()
        {
            try
            {
                int length = 0;
                GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref length); // RelationProcessorCore
                if (length <= 0) return 0;

                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    if (!GetLogicalProcessorInformationEx(0, buffer, ref length)) return 0;

                    int offset = 0;
                    int count = 0;
                    while (offset + 8 <= length)
                    {
                        int size = Marshal.ReadInt32(buffer, offset + 4);
                        if (size <= 0) break;
                        count++;
                        offset += size;
                    }
                    return count;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return 0;
            }
        }

        private static InfoSection BuildCaches(SmbiosStructure processor)
        {
            var section = new InfoSection("Cache");
            var counters = new Dictionary<string, int[]>();
            var order = new List<string>();

            // Кэши через GetLogicalProcessorInformationEx: точные размеры и типы.
            try
            {
                int length = 0;
                GetLogicalProcessorInformationEx(2, IntPtr.Zero, ref length);
                if (length > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal(length);
                    try
                    {
                        if (GetLogicalProcessorInformationEx(2, buffer, ref length))
                        {
                            int offset = 0;
                            while (offset + 8 <= length)
                            {
                                int relationship = Marshal.ReadInt32(buffer, offset);
                                int size = Marshal.ReadInt32(buffer, offset + 4);
                                if (size <= 0) break;

                                int level = Marshal.ReadByte(buffer, offset + 8);
                                int cacheSize = Marshal.ReadInt32(buffer, offset + 12);
                                int cacheType = Marshal.ReadInt32(buffer, offset + 16);

                                string key = CacheLabel(level, cacheType);
                                int[] entry;
                                if (!counters.TryGetValue(key, out entry))
                                {
                                    entry = new int[2];
                                    counters[key] = entry;
                                    order.Add(key);
                                }
                                entry[0] = cacheSize;
                                entry[1]++;

                                offset += size;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch
            {
            }

            if (counters.Count == 0) return null;

            var levels = new List<string>(order);
            levels.Sort(CompareCacheLabels);
            foreach (string key in levels)
            {
                int[] entry = counters[key];
                string value = entry[0] <= 0
                                   ? "\u2014"
                                   : (entry[1] > 1
                                          ? entry[1] + " \u00D7 " + HwFormat.Bytes(entry[0])
                                          : HwFormat.Bytes(entry[0]));
                section.Add(key, value);
            }
            return section;
        }

        private static int CompareCacheLabels(string left, string right)
        {
            int l = left.StartsWith("L1") ? 1 : left.StartsWith("L2") ? 2 : left.StartsWith("L3") ? 3 : 4;
            int r = right.StartsWith("L1") ? 1 : right.StartsWith("L2") ? 2 : right.StartsWith("L3") ? 3 : 4;
            if (l != r) return l.CompareTo(r);
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private static string CacheLabel(int level, int type)
        {
            string suffix = type == 1 ? " Instruction" : type == 2 ? " Data" : string.Empty;
            return "L" + level + suffix;
        }

        private static string BuildInstructions()
        {
            var list = new List<string>();
            AddFeature(list, 3, "MMX");
            AddFeature(list, 6, "SSE");
            AddFeature(list, 10, "SSE2");
            AddFeature(list, 13, "SSE3");
            AddFeature(list, 36, "SSSE3");
            AddFeature(list, 37, "SSE4.1");
            AddFeature(list, 38, "SSE4.2");
            AddFeature(list, 39, "AVX");
            AddFeature(list, 40, "AVX2");
            AddFeature(list, 41, "AVX-512F");
            AddFeature(list, 45, "AVX-512 VBMI");
            AddFeature(list, 47, "AVX-512 VNNI");
            AddFeature(list, 28, "RDRAND");
            AddFeature(list, 32, "RDTSCP");
            AddFeature(list, 22, "RDWRFSGSBASE");
            return string.Join(", ", list.ToArray());
        }

        private static void AddFeature(List<string> list, int feature, string name)
        {
            try
            {
                if (IsProcessorFeaturePresent(feature)) list.Add(name);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------
        // GPU
        // ------------------------------------------------------------------

        public static IList<InfoSection> GetGpuSections()
        {
            var sections = new List<InfoSection>();

            IList<GpuAdapterInfo> adapters = DxgiAdapters.Enumerate();
            int index = 0;
            foreach (GpuAdapterInfo adapter in adapters)
            {
                index++;
                var section = new InfoSection(adapter.Name.Length > 0 ? adapter.Name : "Graphics adapter " + index);

                section.Add("Vendor", Combine(adapter.VendorName,
                    string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", adapter.VendorId)));
                section.Add("Device ID", string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", adapter.DeviceId));
                if (adapter.SubSysId != 0)
                    section.Add("Subsystem ID", string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", adapter.SubSysId));
                if (adapter.Revision != 0)
                    section.Add("Revision", adapter.Revision.ToString(CultureInfo.InvariantCulture));

                bool integrated = adapter.DedicatedVideoMemory < 512L * 1024 * 1024;
                section.Add("Type", integrated ? "Integrated (shared memory)" : "Discrete");
                if (adapter.DedicatedVideoMemory > 0)
                    section.Add("Dedicated VRAM", HwFormat.Bytes(adapter.DedicatedVideoMemory));
                if (adapter.SharedSystemMemory > 0)
                    section.Add("Shared memory", HwFormat.Bytes(adapter.SharedSystemMemory));

                string driver = MachineInfo.GetGpuDriverVersion(adapter.Name);
                if (!string.IsNullOrEmpty(driver)) section.Add("Driver version", driver);

                section.Add("LUID", "0x" + adapter.Luid.ToString("X", CultureInfo.InvariantCulture));
                sections.Add(section);
            }

            if (adapters.Count == 0)
            {
                var none = new InfoSection("Graphics adapter");
                none.Add("Status", "DXGI did not report any adapter", HwLevel.Dim);
                sections.Add(none);
            }

            InfoSection display = BuildDisplay();
            if (display != null) sections.Add(display);

            return sections;
        }

        private static InfoSection BuildDisplay()
        {
            try
            {
                Screen screen = Screen.PrimaryScreen;
                var section = new InfoSection("Display (primary)");
                section.Add("Resolution", screen.Bounds.Width + " \u00D7 " + screen.Bounds.Height);

                DEVMODE mode = new DEVMODE();
                mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, -1, ref mode))
                {
                    section.Add("Refresh rate", mode.dmDisplayFrequency + " Hz");
                    section.Add("Color depth", mode.dmBitsPerPel + " bit");
                }

                section.Add("Monitors", Screen.AllScreens.Length.ToString(CultureInfo.InvariantCulture));
                return section;
            }
            catch
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        // ------------------------------------------------------------------
        // RAM
        // ------------------------------------------------------------------

        public static IList<InfoSection> GetMemorySections()
        {
            var sections = new List<InfoSection>();
            var total = new InfoSection("Summary");

            NativeMethods.MemoryStatusEx memory;
            if (NativeMethods.TryGetMemory(out memory) && memory.TotalPhys > 0)
            {
                const double gb = 1024.0 * 1024 * 1024;
                total.Add("Total", HwFormat.Gb(memory.TotalPhys / gb));
                total.Add("In use", HwFormat.Gb((memory.TotalPhys - memory.AvailPhys) / gb) +
                                     "  (" + HwFormat.Percent(memory.MemoryLoad) + ")");
                total.Add("Available", HwFormat.Gb(memory.AvailPhys / gb));
            }

            SmbiosStructure array = SmbiosReader.First(16);
            if (array != null)
            {
                int devices = array.Word(0x0D);
                if (devices > 0) total.Add("Slots", devices.ToString(CultureInfo.InvariantCulture));
                total.Add("Error correction", MemoryErrorCorrection(array.Byte(0x06)));
            }
            sections.Add(total);

            int moduleIndex = 0;
            foreach (SmbiosStructure module in SmbiosReader.All(17))
            {
                long sizeMb = module.Word(0x0C);
                if (sizeMb == 0x7FFF && module.Data.Length > 0x1F)
                    sizeMb = module.Dword(0x1C);
                if (sizeMb == 0) continue;

                moduleIndex++;
                string locator = module.Text(0x10);
                var section = new InfoSection("Module " + moduleIndex +
                                              (string.IsNullOrEmpty(locator) ? string.Empty : " \u2014 " + locator));
                section.Add("Slot", locator.Length > 0 ? locator : "\u2014");
                string bank = module.Text(0x11);
                if (bank.Length > 0) section.Add("Bank", bank);
                section.Add("Size", HwFormat.Gb(sizeMb / 1024.0));

                string type = MemoryTypeName(module.Byte(0x12));
                if (type.Length > 0) section.Add("Type", type);

                int speed = module.Word(0x15);
                int configured = module.Word(0x20);
                if (speed > 0) section.Add("Speed", HwFormat.Mhz(speed) + " MT/s");
                if (configured > 0) section.Add("Configured speed", HwFormat.Mhz(configured) + " MT/s");

                string manufacturer = module.Text(0x17);
                if (manufacturer.Length > 0) section.Add("Manufacturer", manufacturer);
                string part = module.Text(0x1A);
                if (part.Length > 0) section.Add("Part number", part);
                string serial = module.Text(0x18);
                if (serial.Length > 0) section.Add("Serial", serial);

                string form = FormFactorName(module.Byte(0x0E));
                if (form.Length > 0) section.Add("Form factor", form);

                if (module.Data.Length > 0x1B)
                {
                    int rank = module.Byte(0x1B) & 0x0F;
                    if (rank > 0) section.Add("Rank", rank.ToString(CultureInfo.InvariantCulture) + "R");
                }

                int voltage = module.Data.Length > 0x27 ? module.Word(0x26) : 0;
                if (voltage > 0) section.Add("Voltage", (voltage / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " V");

                int dataWidth = module.Word(0x0A);
                if (dataWidth > 0) section.Add("Data width", dataWidth + " bit");

                sections.Add(section);
            }

            if (moduleIndex == 0)
            {
                var none = new InfoSection("Modules");
                none.Add("Status", "SMBIOS has no memory device records", HwLevel.Dim);
                sections.Add(none);
            }

            return sections;
        }

        private static string MemoryTypeName(byte type)
        {
            switch (type)
            {
                case 0x12: return "DDR";
                case 0x13: return "DDR2";
                case 0x14: return "DDR2 FB-DIMM";
                case 0x18: return "DDR3";
                case 0x1A: return "DDR4";
                case 0x1B: return "LPDDR";
                case 0x1C: return "LPDDR2";
                case 0x1D: return "LPDDR3";
                case 0x1E: return "LPDDR4";
                case 0x22: return "DDR5";
                case 0x23: return "LPDDR5";
                case 0x20: return "HBM";
                case 0x21: return "HBM2";
                default: return type == 0 ? string.Empty : "Type 0x" + type.ToString("X2", CultureInfo.InvariantCulture);
            }
        }

        private static string FormFactorName(byte form)
        {
            switch (form)
            {
                case 0x01: return "Other";
                case 0x02: return "Unknown";
                case 0x03: return "SIMM";
                case 0x04: return "SIP";
                case 0x05: return "Chip";
                case 0x06: return "DIP";
                case 0x07: return "ZIP";
                case 0x08: return "Proprietary Card";
                case 0x09: return "DIMM";
                case 0x0A: return "TSOP";
                case 0x0B: return "Row of chips";
                case 0x0C: return "RIMM";
                case 0x0D: return "SODIMM";
                case 0x0E: return "SRIMM";
                case 0x0F: return "FB-DIMM";
                default: return string.Empty;
            }
        }

        private static string MemoryErrorCorrection(byte value)
        {
            switch (value)
            {
                case 0x03: return "None";
                case 0x04: return "Parity";
                case 0x05: return "Single-bit ECC";
                case 0x06: return "Multi-bit ECC";
                case 0x07: return "CRC";
                default: return "Unknown";
            }
        }

        // ------------------------------------------------------------------
        // Mainboard
        // ------------------------------------------------------------------

        public static IList<InfoSection> GetMainboardSections()
        {
            var sections = new List<InfoSection>();

            SmbiosStructure board = SmbiosReader.First(2);
            if (board != null)
            {
                var section = new InfoSection("Motherboard");
                AddIf(section, "Manufacturer", board.Text(0x04));
                AddIf(section, "Product", board.Text(0x05));
                AddIf(section, "Version", board.Text(0x06));
                AddIf(section, "Serial", board.Text(0x07));
                AddIf(section, "Asset tag", board.Text(0x08));
                AddIf(section, "Board type", BoardTypeName(board.Byte(0x0D)));
                sections.Add(section);
            }

            SmbiosStructure bios = SmbiosReader.First(0);
            if (bios != null)
            {
                var section = new InfoSection("BIOS");
                AddIf(section, "Vendor", bios.Text(0x04));
                AddIf(section, "Version", bios.Text(0x05));
                AddIf(section, "Release date", bios.Text(0x07));
                if (bios.Data.Length > 0x08)
                {
                    int romSize = (bios.Byte(0x08) + 1) * 64;
                    section.Add("ROM size", romSize + " KB");
                }
                section.Add("Firmware mode", FirmwareMode());
                string secureBoot = SecureBootState();
                if (!string.IsNullOrEmpty(secureBoot)) section.Add("Secure Boot", secureBoot);
                sections.Add(section);
            }

            SmbiosStructure system = SmbiosReader.First(1);
            if (system != null)
            {
                var section = new InfoSection("System");
                AddIf(section, "Manufacturer", system.Text(0x04));
                AddIf(section, "Product", system.Text(0x05));
                AddIf(section, "Version", system.Text(0x06));
                AddIf(section, "Serial", system.Text(0x07));
                AddIf(section, "SKU", system.Text(0x19));
                AddIf(section, "Family", system.Text(0x1A));
                sections.Add(section);
            }

            var os = new InfoSection("Operating system");
            AddIf(os, "Name", MachineInfo.OsProduct);
            AddIf(os, "Version", MachineInfo.OsVersion);
            AddIf(os, "Build", MachineInfo.OsBuild);
            os.Add("Architecture", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
            os.Add("Uptime", HwFormat.Uptime(NativeMethods.Uptime));
            os.Add("Machine", Environment.MachineName);
            sections.Add(os);

            var slots = new InfoSection("Slots");
            foreach (SmbiosStructure slot in SmbiosReader.All(9))
            {
                string name = slot.Text(0x04);
                if (name.Length == 0) name = "Slot";
                string type = SlotTypeName(slot.Byte(0x05));
                string usage = SlotUsageName(slot.Byte(0x07));
                string value = Combine(type, usage);
                slots.Add(name, value.Length > 0 ? value : "\u2014");
            }
            if (slots.Rows.Count > 0) sections.Add(slots);

            return sections;
        }

        private static void AddIf(InfoSection section, string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) section.Add(label, value);
        }

        private static string BoardTypeName(byte type)
        {
            switch (type)
            {
                case 0x01: return "Other";
                case 0x02: return "Unknown";
                case 0x03: return "Server Blade";
                case 0x04: return "Connectivity Switch";
                case 0x05: return "System Management Module";
                case 0x06: return "Processor Module";
                case 0x07: return "I/O Module";
                case 0x08: return "Memory Module";
                case 0x09: return "Daughter Board";
                case 0x0A: return "Motherboard";
                case 0x0B: return "Processor/Memory Module";
                case 0x0C: return "Processor/IO Module";
                case 0x0D: return "Interconnect Board";
                default: return string.Empty;
            }
        }

        private static string FirmwareMode()
        {
            try
            {
                FirmwareType type;
                if (GetFirmwareType(out type))
                {
                    if (type == FirmwareType.Uefi) return "UEFI";
                    if (type == FirmwareType.Bios) return "Legacy BIOS";
                }
            }
            catch
            {
            }
            return "Unknown";
        }

        private static string SecureBootState()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                {
                    if (key == null) return string.Empty;
                    object value = key.GetValue("UEFISecureBootEnabled");
                    if (value == null) return string.Empty;
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1 ? "Enabled" : "Disabled";
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private enum FirmwareType
        {
            Unknown = 0,
            Bios = 1,
            Uefi = 2,
            Max = 3
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetFirmwareType(out FirmwareType firmwareType);

        private static string SlotTypeName(byte type)
        {
            switch (type)
            {
                case 0x06: return "PCI";
                case 0x07: return "AGP";
                case 0x0A: return "PCI-X";
                case 0xA5: return "PCIe x1";
                case 0xA6: return "PCIe x2";
                case 0xA7: return "PCIe x4";
                case 0xA8: return "PCIe x8";
                case 0xA9: return "PCIe x16";
                case 0xAA: return "PCIe Mini";
                case 0xAB: return "PCIe M.2";
                case 0x0C: return "PCIe";
                default: return type == 0 ? string.Empty : "Type 0x" + type.ToString("X2", CultureInfo.InvariantCulture);
            }
        }

        private static string SlotUsageName(byte usage)
        {
            switch (usage)
            {
                case 0x03: return "available";
                case 0x04: return "in use";
                case 0x05: return "unavailable";
                default: return string.Empty;
            }
        }

        // ------------------------------------------------------------------
        // Storage
        // ------------------------------------------------------------------

        public static IList<InfoSection> GetStorageSections()
        {
            var sections = new List<InfoSection>();
            IList<StorageProbe.DiskInfo> disks;
            try
            {
                disks = new StorageProbe().Read();
            }
            catch
            {
                disks = new List<StorageProbe.DiskInfo>();
            }

            foreach (StorageProbe.DiskInfo disk in disks)
            {
                string title = string.IsNullOrEmpty(disk.Model) ? "Disk " + disk.Number : disk.Model;
                var section = new InfoSection(title, true);

                section.Add("Interface", disk.Bus.Length > 0 ? disk.Bus : "n/a");
                section.Add("Capacity", disk.Size > 0 ? HwFormat.Bytes(disk.Size) : "n/a");
                if (!string.IsNullOrEmpty(disk.Serial)) section.Add("Serial", disk.Serial);

                DiskHealth health = StorageSmart.Read(disk.Number, disk.Bus, disk.Size);
                if (health.LogicalSector > 0)
                    section.Add("Logical sector", health.LogicalSector + " bytes");
                section.Add("Partition table", health.PartitionStyle);

                if (health.Nvme != null && health.Nvme.Available)
                {
                    NvmeHealth nvme = health.Nvme;
                    if (!string.IsNullOrEmpty(nvme.Firmware)) section.Add("Firmware", nvme.Firmware);
                    section.Add("Temperature", HwFormat.Celsius(nvme.TemperatureC), TemperatureLevel(nvme.TemperatureC));
                    section.Add("Health", nvme.PercentageUsed + " % used, " +
                                         nvme.AvailableSpare + " % spare",
                                nvme.PercentageUsed >= 90 ? HwLevel.Warn : HwLevel.Normal);
                    section.Add("Critical warning", "0x" + nvme.CriticalWarning.ToString("X2", CultureInfo.InvariantCulture));
                    section.Add("Spare threshold", nvme.SpareThreshold + " %");
                    section.Add("Data read", HwFormat.Bytes(SafeLong(nvme.DataReadBytes)));
                    section.Add("Data written", HwFormat.Bytes(SafeLong(nvme.DataWrittenBytes)));
                    section.Add("Power cycles", nvme.PowerCycles.ToString(CultureInfo.InvariantCulture));
                    section.Add("Power-on hours", nvme.PowerOnHours + " h");
                    section.Add("Unsafe shutdowns", nvme.UnsafeShutdowns.ToString(CultureInfo.InvariantCulture));
                    section.Add("Media errors", nvme.MediaErrors.ToString(CultureInfo.InvariantCulture),
                                nvme.MediaErrors > 0 ? HwLevel.Warn : HwLevel.Normal);
                    section.Add("Error log entries", nvme.ErrorLogEntries.ToString(CultureInfo.InvariantCulture));
                }
                else if (health.Ata != null && health.Ata.Available)
                {
                    foreach (SmartAttribute attribute in health.Ata.Attributes)
                    {
                        if (attribute.Id == 0) continue;
                        string label = attribute.Id.ToString("00", CultureInfo.InvariantCulture) + "  " + attribute.Name;
                        string value = attribute.Value + " / " + attribute.Worst + " / " + attribute.Threshold +
                                       "   raw " + attribute.Raw;
                        HwLevel level = attribute.Threshold > 0 && attribute.Value <= attribute.Threshold
                                            ? HwLevel.Warn
                                            : HwLevel.Normal;
                        section.Add(label, value, level);
                    }
                }
                else
                {
                    string error = health.Nvme != null ? health.Nvme.Error : (health.Ata != null ? health.Ata.Error : string.Empty);
                    section.Add("SMART", string.IsNullOrEmpty(error) ? "not available" : error, HwLevel.Dim);
                }

                foreach (StorageProbe.VolumeInfo volume in disk.Volumes)
                {
                    string name = volume.Root + (volume.Label.Length > 0 ? "  " + volume.Label : string.Empty);
                    section.Add(name, HwFormat.Bytes(volume.Free) + " free of " + HwFormat.Bytes(volume.Total) +
                                      (volume.Format.Length > 0 ? "  (" + volume.Format + ")" : string.Empty));
                }

                sections.Add(section);
            }

            if (sections.Count == 0)
            {
                var none = new InfoSection("Storage");
                none.Add("Status", "no disks reported", HwLevel.Dim);
                sections.Add(none);
            }

            return sections;
        }

        private static HwLevel TemperatureLevel(double celsius)
        {
            if (celsius >= 70) return HwLevel.Warn;
            if (celsius >= 85) return HwLevel.Critical;
            return HwLevel.Normal;
        }

        private static long SafeLong(ulong value)
        {
            return value > long.MaxValue ? long.MaxValue : (long)value;
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

        // ------------------------------------------------------------------
        // Native helpers
        // ------------------------------------------------------------------

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref int length);

        [DllImport("kernel32.dll")]
        private static extern bool IsProcessorFeaturePresent(int feature);
    }
}
