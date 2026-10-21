using System;
using System.Runtime.InteropServices;

namespace Berlit
{
    /// <summary>
    /// Вся работа с Win32 API собрана здесь: память, аптайм, оформление окна,
    /// запросы к накопителям. Никаких внешних зависимостей — только системные DLL.
    /// </summary>
    internal static class NativeMethods
    {
        // ------------------------------------------------------------------
        // Оперативная память
        // ------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        /// <summary>Реальные данные о физической памяти (не требует прав администратора).</summary>
        public static bool TryGetMemory(out MemoryStatusEx info)
        {
            info = new MemoryStatusEx();
            info.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            try
            {
                return GlobalMemoryStatusEx(ref info);
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Время работы системы
        // ------------------------------------------------------------------

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static TimeSpan Uptime
        {
            get
            {
                try
                {
                    long ms = (long)GetTickCount64();
                    if (ms < 0 || ms > TimeSpan.MaxValue.TotalMilliseconds) return TimeSpan.Zero;
                    return TimeSpan.FromMilliseconds(ms);
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        // ------------------------------------------------------------------
        // Тёмное оформление системных элементов окна
        // ------------------------------------------------------------------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Тёмный заголовок окна (Windows 10 1809+ / Windows 11).</summary>
        public static void ApplyDarkTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int on = 1;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE, 19 = то же для ранних сборок Windows 10.
                if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
            }
            catch
            {
                // Сторонняя тема или старая ОС — просто остаётся стандартный заголовок.
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        /// <summary>Тёмные полосы прокрутки для системных контролов (если тема поддерживает).</summary>
        public static void ApplyDarkScrollbars(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                SetWindowTheme(hwnd, "DarkMode_Explorer", null);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------
        // Накопители: IOCTL-запросы к тому и физическому диску
        // ------------------------------------------------------------------

        internal const uint GENERIC_READ = 0x80000000;
        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint OPEN_EXISTING = 3;

        internal const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
        internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        internal const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security,
                                                 uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DeviceIoControl(IntPtr handle, uint code, IntPtr inBuffer, int inSize,
                                                    IntPtr outBuffer, int outSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);

        internal static bool IsInvalidHandle(IntPtr handle)
        {
            return handle == IntPtr.Zero || handle == new IntPtr(-1);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct StorageDeviceNumber
        {
            public uint DeviceType;
            public uint DeviceNumber;
            public uint PartitionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct StoragePropertyQuery
        {
            public uint PropertyId;
            public uint QueryType;
            public uint AdditionalParameters0;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct StorageDeviceDescriptorHeader
        {
            public uint Version;
            public uint Size;
            public byte DeviceType;
            public byte DeviceTypeModifier;
            [MarshalAs(UnmanagedType.U1)] public bool RemovableMedia;
            [MarshalAs(UnmanagedType.U1)] public bool CommandQueueing;
            public uint VendorIdOffset;
            public uint ProductIdOffset;
            public uint ProductRevisionOffset;
            public uint SerialNumberOffset;
            public uint BusType;
            public uint RawPropertiesLength;
        }

        /// <summary>Тип шины из STORAGE_DEVICE_DESCRIPTOR.</summary>
        public static string BusName(uint busType)
        {
            switch (busType)
            {
                case 1: return "SCSI";
                case 2: return "ATAPI";
                case 3: return "ATA";
                case 4: return "1394";
                case 5: return "SSA";
                case 6: return "Fibre";
                case 7: return "USB";
                case 8: return "RAID";
                case 9: return "iSCSI";
                case 10: return "SAS";
                case 11: return "SATA";
                case 12: return "SD";
                case 13: return "MMC";
                case 14: return "Virtual";
                case 15: return "File-backed";
                case 16: return "Storage Spaces";
                case 17: return "NVMe";
                case 18: return "SCM";
                case 19: return "UFS";
                default: return string.Empty;
            }
        }
    }
}
