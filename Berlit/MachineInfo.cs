using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Berlit
{
    /// <summary>
    /// Статичные сведения о машине из реестра. Это самый дешёвый и самый
    /// надёжный источник: работает без службы WMI, без прав администратора
    /// и без внешних библиотек.
    /// </summary>
    internal static class MachineInfo
    {
        private static readonly object Sync = new object();
        private static bool _loaded;

        public static string CpuName { get; private set; }
        public static string CpuIdentifier { get; private set; }
        public static double CpuNominalMhz { get; private set; }
        public static string CpuVendor { get; private set; }

        public static string BoardManufacturer { get; private set; }
        public static string BoardProduct { get; private set; }
        public static string BoardVersion { get; private set; }
        public static string BoardSerial { get; private set; }

        public static string SystemManufacturer { get; private set; }
        public static string SystemProduct { get; private set; }
        public static string SystemSerial { get; private set; }

        public static string BiosVendor { get; private set; }
        public static string BiosVersion { get; private set; }
        public static string BiosDate { get; private set; }

        public static string OsProduct { get; private set; }
        public static string OsVersion { get; private set; }
        public static string OsBuild { get; private set; }

        private static readonly Dictionary<string, string> GpuDriverVersions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool _gpuDriversLoaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Sync)
            {
                if (_loaded) return;

                const string cpuPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
                CpuName = ReadString(cpuPath, "ProcessorNameString");
                CpuIdentifier = ReadString(cpuPath, "Identifier");
                CpuVendor = ReadString(cpuPath, "VendorIdentifier");
                CpuNominalMhz = ReadInt(cpuPath, "~MHz", 0);

                const string biosPath = @"HARDWARE\DESCRIPTION\System\BIOS";
                BoardManufacturer = ReadString(biosPath, "BaseBoardManufacturer");
                BoardProduct = ReadString(biosPath, "BaseBoardProduct");
                BoardVersion = ReadString(biosPath, "BaseBoardVersion");
                BoardSerial = ReadString(biosPath, "BaseBoardSerialNumber");
                SystemManufacturer = ReadString(biosPath, "SystemManufacturer");
                SystemProduct = ReadString(biosPath, "SystemProductName");
                SystemSerial = ReadString(biosPath, "SystemSerialNumber");
                BiosVendor = ReadString(biosPath, "BIOSVendor");
                BiosVersion = ReadString(biosPath, "BIOSVersion");
                BiosDate = ReadString(biosPath, "BIOSReleaseDate");

                const string osPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                OsProduct = ReadString(osPath, "ProductName");
                OsVersion = ReadString(osPath, "DisplayVersion");
                string build = ReadString(osPath, "CurrentBuild");
                string ubr = ReadString(osPath, "UBR");
                OsBuild = string.IsNullOrEmpty(build) ? string.Empty :
                          (string.IsNullOrEmpty(ubr) ? build : build + "." + ubr);

                _loaded = true;
            }
        }

        /// <summary>Версия драйвера видеоадаптера по его имени (из ключей класса дисплея).</summary>
        public static string GetGpuDriverVersion(string adapterName)
        {
            if (string.IsNullOrEmpty(adapterName)) return string.Empty;
            lock (Sync)
            {
                if (!_gpuDriversLoaded)
                {
                    _gpuDriversLoaded = true;
                    const string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(classPath))
                        {
                            if (key != null)
                            {
                                foreach (string name in key.GetSubKeyNames())
                                {
                                    if (name.Length != 4) continue;      // 0000, 0001, ...
                                    using (RegistryKey sub = key.OpenSubKey(name))
                                    {
                                        if (sub == null) continue;
                                        string desc = sub.GetValue("DriverDesc") as string;
                                        string version = sub.GetValue("DriverVersion") as string;
                                        if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(version))
                                            GpuDriverVersions[desc.Trim()] = version.Trim();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                string direct;
                if (GpuDriverVersions.TryGetValue(adapterName.Trim(), out direct)) return direct;

                // Иногда имя в реестре — префикс имени из DXGI (или наоборот).
                foreach (KeyValuePair<string, string> pair in GpuDriverVersions)
                {
                    if (adapterName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pair.Key.IndexOf(adapterName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return pair.Value;
                }
            }
            return string.Empty;
        }

        private static string ReadString(string path, string name)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key == null) return string.Empty;
                    object value = key.GetValue(name);
                    return value == null ? string.Empty : value.ToString().Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static double ReadInt(string path, string name, double fallback)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key == null) return fallback;
                    object value = key.GetValue(name);
                    if (value == null) return fallback;
                    double parsed;
                    if (double.TryParse(value.ToString(), out parsed)) return parsed;
                }
            }
            catch
            {
            }
            return fallback;
        }
    }
}
