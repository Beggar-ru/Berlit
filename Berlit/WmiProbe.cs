using System;
using System.Collections.Generic;
using System.Management;

namespace Berlit
{
    /// <summary>
    /// Необязательное обогащение через WMI: модули памяти, число физических ядер,
    /// сокет, кэш. На многих машинах WMI закрыт политикой или служба отключена,
    /// поэтому все методы построены как «попробовать и не мешать»:
    /// при первой же ошибке опрос отключается на 5 минут, приложение продолжает
    /// работать на реестре и счётчиках производительности.
    /// </summary>
    internal sealed class WmiProbe
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ManagementScope> _scopes = new Dictionary<string, ManagementScope>();
        private DateTime _retryAfter = DateTime.MinValue;
        private string _error = string.Empty;
        private bool _checked;
        private bool _available;

        public bool Available
        {
            get { lock (_sync) { return _available; } }
        }

        public string Error
        {
            get { lock (_sync) { return _error; } }
        }

        public bool Checked
        {
            get { lock (_sync) { return _checked; } }
        }

        private ManagementScope GetScope(string ns)
        {
            ManagementScope scope;
            if (_scopes.TryGetValue(ns, out scope)) return scope;
            scope = new ManagementScope(@"\\.\" + ns);
            scope.Connect();
            _scopes[ns] = scope;
            return scope;
        }

        /// <summary>
        /// Возвращает строки запроса. null — WMI недоступен, пустой список — данных нет.
        /// </summary>
        public List<Dictionary<string, object>> Query(string ns, string wql, params string[] properties)
        {
            lock (_sync)
            {
                _checked = true;
                if (_retryAfter > DateTime.UtcNow)
                {
                    _available = false;
                    return null;
                }

                try
                {
                    ManagementScope scope = GetScope(ns);
                    var rows = new List<Dictionary<string, object>>();
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql)))
                    using (ManagementObjectCollection items = searcher.Get())
                    {
                        foreach (ManagementBaseObject item in items)
                        {
                            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            foreach (string property in properties)
                            {
                                try { row[property] = item[property]; }
                                catch { row[property] = null; }
                            }
                            rows.Add(row);
                        }
                    }
                    _available = true;
                    _error = string.Empty;
                    return rows;
                }
                catch (Exception ex)
                {
                    _available = false;
                    _retryAfter = DateTime.UtcNow.AddMinutes(5);
                    _error = ShortError(ex);
                    return null;
                }
            }
        }

        private static string ShortError(Exception ex)
        {
            string text = ex.Message ?? string.Empty;
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 90 ? text.Substring(0, 90) : text;
        }

        public sealed class MemoryModule
        {
            public string Locator = string.Empty;
            public string Bank = string.Empty;
            public string Manufacturer = string.Empty;
            public string PartNumber = string.Empty;
            public string TypeName = string.Empty;
            public long Capacity;
            public int Speed;
            public int ConfiguredSpeed;
        }

        public List<MemoryModule> GetMemoryModules()
        {
            List<Dictionary<string, object>> rows = Query("root\\cimv2",
                "SELECT DeviceLocator,BankLabel,Capacity,Speed,ConfiguredClockSpeed,Manufacturer,PartNumber,SMBIOSMemoryType " +
                "FROM Win32_PhysicalMemory",
                "DeviceLocator", "BankLabel", "Capacity", "Speed", "ConfiguredClockSpeed",
                "Manufacturer", "PartNumber", "SMBIOSMemoryType");

            var result = new List<MemoryModule>();
            if (rows == null) return result;

            foreach (Dictionary<string, object> row in rows)
            {
                var module = new MemoryModule();
                module.Locator = Text(row, "DeviceLocator");
                if (module.Locator.Length == 0) module.Locator = Text(row, "BankLabel");
                module.Bank = Text(row, "BankLabel");
                module.Manufacturer = Text(row, "Manufacturer");
                module.PartNumber = Text(row, "PartNumber");
                module.Capacity = (long)Number(row, "Capacity", 0);
                module.Speed = (int)Number(row, "Speed", 0);
                module.ConfiguredSpeed = (int)Number(row, "ConfiguredClockSpeed", 0);
                module.TypeName = MemoryTypeName((int)Number(row, "SMBIOSMemoryType", 0));
                if (module.Locator.Length == 0) module.Locator = "DIMM";
                result.Add(module);
            }
            return result;
        }

        public sealed class CpuDetails
        {
            public string Name = string.Empty;
            public string Socket = string.Empty;
            public int Cores;
            public int Logical;
            public double MaxClockMhz;
            public long L2;
            public long L3;
        }

        public CpuDetails GetCpuDetails()
        {
            List<Dictionary<string, object>> rows = Query("root\\cimv2",
                "SELECT Name,SocketDesignation,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,L2CacheSize,L3CacheSize " +
                "FROM Win32_Processor",
                "Name", "SocketDesignation", "NumberOfCores", "NumberOfLogicalProcessors",
                "MaxClockSpeed", "L2CacheSize", "L3CacheSize");
            if (rows == null || rows.Count == 0) return null;

            Dictionary<string, object> row = rows[0];
            var details = new CpuDetails
            {
                Name = Text(row, "Name"),
                Socket = Text(row, "SocketDesignation"),
                Cores = (int)Number(row, "NumberOfCores", 0),
                Logical = (int)Number(row, "NumberOfLogicalProcessors", 0),
                MaxClockMhz = Number(row, "MaxClockSpeed", 0),
                L2 = (long)Number(row, "L2CacheSize", 0) * 1024,
                L3 = (long)Number(row, "L3CacheSize", 0) * 1024
            };
            return details;
        }

        private static string Text(Dictionary<string, object> row, string key)
        {
            object value;
            if (!row.TryGetValue(key, out value) || value == null) return string.Empty;
            string text = value.ToString();
            return text == null ? string.Empty : text.Trim();
        }

        private static double Number(Dictionary<string, object> row, string key, double fallback)
        {
            object value;
            if (!row.TryGetValue(key, out value) || value == null) return fallback;
            try
            {
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string MemoryTypeName(int type)
        {
            switch (type)
            {
                case 20: return "DDR";
                case 21: return "DDR2";
                case 22: return "DDR2 FB-DIMM";
                case 24: return "DDR3";
                case 26: return "DDR4";
                case 27: return "LPDDR";
                case 28: return "LPDDR2";
                case 29: return "LPDDR3";
                case 30: return "LPDDR4";
                case 34: return "DDR5";
                default: return string.Empty;
            }
        }
    }
}
