using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Berlit
{
    internal sealed class NvmeHealth
    {
        public bool Available;
        public string Error = string.Empty;
        public string Model = string.Empty;
        public string Serial = string.Empty;
        public string Firmware = string.Empty;

        public int CriticalWarning;
        public int TemperatureC;
        public int AvailableSpare;
        public int SpareThreshold;
        public int PercentageUsed;

        public ulong DataReadBytes;
        public ulong DataWrittenBytes;
        public ulong PowerCycles;
        public ulong PowerOnHours;
        public ulong UnsafeShutdowns;
        public ulong MediaErrors;
        public ulong ErrorLogEntries;
        public ulong WarningTempMinutes;
        public ulong CriticalTempMinutes;
    }

    internal sealed class SmartAttribute
    {
        public int Id;
        public string Name = string.Empty;
        public int Value;
        public int Worst;
        public int Threshold;
        public string Raw = string.Empty;
    }

    internal sealed class AtaHealth
    {
        public bool Available;
        public string Error = string.Empty;
        public readonly List<SmartAttribute> Attributes = new List<SmartAttribute>();
    }

    internal sealed class DiskHealth
    {
        public int Number;
        public long Size;
        public int LogicalSector;
        public string PartitionStyle = "n/a";
        public NvmeHealth Nvme;
        public AtaHealth Ata;
    }

    /// <summary>
    /// Низкоуровневые запросы к накопителям: геометрия, таблица разделов,
    /// NVMe Identify и SMART/Health, SATA SMART через ATA pass-through.
    /// NVMe читается без прав администратора, SATA SMART — обычно только с ними.
    /// </summary>
    internal static class StorageSmart
    {
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;
        private const uint OPEN_EXISTING = 3;

        private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
        private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x700A0;
        private const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x70050;
        private const uint IOCTL_ATA_PASS_THROUGH = 0x4D02C;

        private const uint StorageDeviceProtocolSpecificProperty = 49;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security,
                                                uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr handle, uint code, IntPtr inBuffer, int inSize,
                                                   IntPtr outBuffer, int outSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AtaPassThrough
        {
            public ushort Length;
            public ushort AtaFlags;
            public byte PathId;
            public byte TargetId;
            public byte Lun;
            public byte ReservedAsUchar;
            public uint DataTransferLength;
            public uint TimeOutValue;
            public uint ReservedAsUlong;
            public uint DataBufferOffset;
            public byte PreviousTaskFile0;
            public byte PreviousTaskFile1;
            public byte PreviousTaskFile2;
            public byte PreviousTaskFile3;
            public byte PreviousTaskFile4;
            public byte PreviousTaskFile5;
            public byte PreviousTaskFile6;
            public byte PreviousTaskFile7;
            public byte CurrentTaskFile0;
            public byte CurrentTaskFile1;
            public byte CurrentTaskFile2;
            public byte CurrentTaskFile3;
            public byte CurrentTaskFile4;
            public byte CurrentTaskFile5;
            public byte CurrentTaskFile6;
            public byte CurrentTaskFile7;
        }

        public static DiskHealth Read(int number, string bus, long size)
        {
            var health = new DiskHealth { Number = number, Size = size };
            string path = @"\\.\PHYSICALDRIVE" + number.ToString(CultureInfo.InvariantCulture);

            IntPtr handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                health.PartitionStyle = "n/a";
                return health;
            }

            try
            {
                ReadGeometry(handle, health);
                ReadPartitionStyle(handle, health);

                if (string.Equals(bus, "NVMe", StringComparison.OrdinalIgnoreCase))
                    health.Nvme = ReadNvme(handle);
                else
                {
                    health.Ata = ReadAta(handle);
                    // У USB-мостов SMART часто недоступен — не считаем это ошибкой.
                }
            }
            catch
            {
            }
            finally
            {
                CloseHandle(handle);
            }

            return health;
        }

        private static void ReadGeometry(IntPtr handle, DiskHealth health)
        {
            const int bufferSize = 256;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int returned;
                if (!DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0,
                        buffer, bufferSize, out returned, IntPtr.Zero) || returned < 32)
                    return;

                health.LogicalSector = Marshal.ReadInt32(buffer, 20);
                long size = Marshal.ReadInt64(buffer, 24);
                if (size > 0 && health.Size <= 0) health.Size = size;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void ReadPartitionStyle(IntPtr handle, DiskHealth health)
        {
            const int bufferSize = 65536;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int returned;
                if (!DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0,
                        buffer, bufferSize, out returned, IntPtr.Zero) || returned < 8)
                {
                    health.PartitionStyle = "n/a";
                    return;
                }

                int style = Marshal.ReadInt32(buffer, 0);
                health.PartitionStyle = style == 0 ? "MBR" : (style == 1 ? "GPT" : "RAW");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ------------------------------------------------------------------
        // NVMe: Identify Controller + SMART/Health log page
        // ------------------------------------------------------------------

        private static NvmeHealth ReadNvme(IntPtr handle)
        {
            var health = new NvmeHealth();

            string error;
            byte[] identify = QueryNvme(handle, 1, 1, 4096, out error);
            if (identify == null)
            {
                health.Error = error;
                return health;
            }

            health.Model = Ascii(identify, 24, 40);
            health.Serial = Ascii(identify, 4, 20);
            health.Firmware = Ascii(identify, 64, 8);

            byte[] smart = QueryNvme(handle, 2, 2, 512, out error);
            if (smart == null)
            {
                health.Error = error;
                return health;
            }

            health.Available = true;
            health.CriticalWarning = smart[0];
            int kelvin = smart[1] | (smart[2] << 8);
            health.TemperatureC = kelvin - 273;
            health.AvailableSpare = smart[3];
            health.SpareThreshold = smart[4];
            health.PercentageUsed = smart[5];

            // 128-битные счётчики: для реальных значений достаточно младших 64 бит.
            ulong unitsRead = ReadUInt64(smart, 32);
            ulong unitsWritten = ReadUInt64(smart, 48);
            health.DataReadBytes = unitsRead * 512000UL;
            health.DataWrittenBytes = unitsWritten * 512000UL;
            health.PowerCycles = ReadUInt64(smart, 112);
            health.PowerOnHours = ReadUInt64(smart, 128);
            health.UnsafeShutdowns = ReadUInt64(smart, 144);
            health.MediaErrors = ReadUInt64(smart, 160);
            health.ErrorLogEntries = ReadUInt64(smart, 176);
            health.WarningTempMinutes = ReadUInt64(smart, 192);
            health.CriticalTempMinutes = ReadUInt64(smart, 196);
            return health;
        }

        /// <summary>Запрос данных NVMe через STORAGE_PROPERTY_QUERY (работает без администратора).</summary>
        private static byte[] QueryNvme(IntPtr handle, int dataType, uint requestValue, int dataLength, out string error)
        {
            error = string.Empty;
            const int headerSize = 8;   // PSD начинается сразу после PropertyId/QueryType
            int inSize = headerSize + 40;
            int outSize = 64 + dataLength;

            IntPtr input = Marshal.AllocHGlobal(inSize);
            IntPtr output = Marshal.AllocHGlobal(outSize);
            try
            {
                for (int i = 0; i < inSize; i++) Marshal.WriteByte(input, i, 0);
                Marshal.WriteInt32(input, 0, (int)StorageDeviceProtocolSpecificProperty);
                Marshal.WriteInt32(input, 4, 0);            // PropertyStandardQuery
                Marshal.WriteInt32(input, 8, 3);            // ProtocolTypeNvme
                Marshal.WriteInt32(input, 12, dataType);    // 1 = Identify, 2 = LogPage
                Marshal.WriteInt32(input, 16, (int)requestValue);
                Marshal.WriteInt32(input, 20, 0);
                Marshal.WriteInt32(input, 24, 40);          // ProtocolDataOffset
                Marshal.WriteInt32(input, 28, dataLength);  // ProtocolDataLength

                int returned;
                bool ok = DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY, input, inSize,
                                          output, outSize, out returned, IntPtr.Zero);
                if (!ok)
                {
                    int code = Marshal.GetLastWin32Error();
                    error = code == 5 ? "access denied" : "error " + code;
                    return null;
                }

                if (returned < 40) { error = "short response"; return null; }

                // ProtocolDataOffset отсчитывается от начала STORAGE_PROTOCOL_SPECIFIC_DATA,
                // а она сама начинается с 8-го байта выходного дескриптора.
                int relative = Marshal.ReadInt32(output, 24);
                int length = Marshal.ReadInt32(output, 28);
                int offset = 8 + relative;
                if (relative <= 0 || length <= 0 || offset + length > returned)
                {
                    offset = 48;
                    length = Math.Min(dataLength, returned - offset);
                }
                if (length <= 0) { error = "no data"; return null; }

                byte[] data = new byte[length];
                Marshal.Copy(new IntPtr(output.ToInt64() + offset), data, 0, length);
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(input);
                Marshal.FreeHGlobal(output);
            }
        }

        private static string Ascii(byte[] data, int offset, int length)
        {
            if (offset < 0 || offset + length > data.Length) return string.Empty;
            return Encoding.ASCII.GetString(data, offset, length).Trim('\0', ' ');
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            if (offset < 0 || offset + 8 > data.Length) return 0;
            ulong value = 0;
            for (int i = 7; i >= 0; i--) value = (value << 8) | data[offset + i];
            return value;
        }

        // ------------------------------------------------------------------
        // SATA: SMART READ DATA / READ THRESHOLDS
        // ------------------------------------------------------------------

        private static AtaHealth ReadAta(IntPtr handle)
        {
            var health = new AtaHealth();

            byte[] data;
            string error;
            if (!TryAtaCommand(handle, 0xD0, out data, out error))
            {
                health.Error = error;
                return health;
            }

            byte[] thresholds = null;
            string thresholdError;
            TryAtaCommand(handle, 0xD1, out thresholds, out thresholdError);

            health.Available = true;
            for (int i = 0; i < 30; i++)
            {
                int offset = 2 + i * 12;
                if (offset + 12 > data.Length) break;

                int id = data[offset];
                if (id == 0) continue;

                var attribute = new SmartAttribute
                {
                    Id = id,
                    Name = SmartNames(id),
                    Value = data[offset + 3],
                    Worst = data[offset + 4],
                    Raw = FormatRaw(data, offset + 5)
                };

                if (thresholds != null && offset + 1 < thresholds.Length)
                    attribute.Threshold = thresholds[offset + 1];

                health.Attributes.Add(attribute);
            }
            return health;
        }

        private static bool TryAtaCommand(IntPtr handle, byte feature, out byte[] data, out string error)
        {
            data = null;
            error = string.Empty;

            int headerSize = Marshal.SizeOf(typeof(AtaPassThrough));
            int total = headerSize + 512;
            IntPtr buffer = Marshal.AllocHGlobal(total);
            try
            {
                for (int i = 0; i < total; i++) Marshal.WriteByte(buffer, i, 0);

                var command = new AtaPassThrough
                {
                    Length = (ushort)headerSize,
                    AtaFlags = 0x02,                    // ATA_FLAGS_DATA_IN
                    DataTransferLength = 512,
                    TimeOutValue = 10,
                    DataBufferOffset = (uint)headerSize
                };
                command.CurrentTaskFile0 = feature;    // Features: 0xD0 SMART READ DATA, 0xD1 THRESHOLDS
                command.CurrentTaskFile1 = 0x01;       // Sector count
                command.CurrentTaskFile2 = 0x4F;       // LBA low
                command.CurrentTaskFile3 = 0xC2;       // LBA mid
                command.CurrentTaskFile4 = 0xA0;       // LBA high
                command.CurrentTaskFile5 = 0xB0;       // Device
                command.CurrentTaskFile6 = 0xB0;       // Command: SMART

                Marshal.StructureToPtr(command, buffer, false);

                int returned;
                bool ok = DeviceIoControl(handle, IOCTL_ATA_PASS_THROUGH, buffer, total,
                                          buffer, total, out returned, IntPtr.Zero);
                if (!ok)
                {
                    int code = Marshal.GetLastWin32Error();
                    error = code == 5 ? "requires administrator" : "error " + code;
                    return false;
                }

                data = new byte[512];
                Marshal.Copy(new IntPtr(buffer.ToInt64() + headerSize), data, 0, 512);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string FormatRaw(byte[] data, int offset)
        {
            if (offset + 6 > data.Length) return string.Empty;

            ulong value = 0;
            for (int i = 5; i >= 0; i--) value = (value << 8) | data[offset + i];

            // Младшие 16 бит часто несут само значение (температура и т.п.),
            // старшие — дополнительные счётчики. Показываем оба, как CrystalDiskInfo.
            int low = data[offset] | (data[offset + 1] << 8);
            if (value > ushort.MaxValue)
                return value.ToString(CultureInfo.InvariantCulture) + " (" + low.ToString(CultureInfo.InvariantCulture) + ")";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 1, "Read Error Rate" },
            { 2, "Throughput Performance" },
            { 3, "Spin-Up Time" },
            { 4, "Start/Stop Count" },
            { 5, "Reallocated Sectors Count" },
            { 7, "Seek Error Rate" },
            { 8, "Seek Time Performance" },
            { 9, "Power-On Hours" },
            { 10, "Spin Retry Count" },
            { 11, "Calibration Retry Count" },
            { 12, "Power Cycle Count" },
            { 13, "Soft Read Error Rate" },
            { 22, "Helium Level" },
            { 100, "GB Erased" },
            { 160, "Uncorrectable Error Count" },
            { 163, "Valid Spare Blocks" },
            { 164, "Total Erase Count" },
            { 167, "Average Erase Count" },
            { 168, "Max Bad Block Count" },
            { 169, "Remaining Life" },
            { 170, "Bad Block Count" },
            { 171, "Program Fail Count" },
            { 172, "Erase Fail Count" },
            { 173, "Wear Leveling Count" },
            { 174, "Unexpected Power Loss" },
            { 177, "Wear Leveling Count" },
            { 178, "Used Reserved Block Count" },
            { 179, "Used Reserved Block Count Total" },
            { 180, "Unused Reserved Block Count" },
            { 181, "Program Fail Count" },
            { 182, "Erase Fail Count" },
            { 183, "Runtime Bad Block" },
            { 184, "End-to-End Error" },
            { 187, "Reported Uncorrectable Errors" },
            { 188, "Command Timeout" },
            { 189, "High Fly Writes" },
            { 190, "Airflow Temperature" },
            { 191, "G-Sense Error Rate" },
            { 192, "Power-Off Retract Count" },
            { 193, "Load/Unload Cycle Count" },
            { 194, "Temperature" },
            { 195, "Hardware ECC Recovered" },
            { 196, "Reallocation Event Count" },
            { 197, "Current Pending Sector Count" },
            { 198, "Uncorrectable Sector Count" },
            { 199, "UltraDMA CRC Error Count" },
            { 200, "Multi-Zone Error Rate" },
            { 201, "Soft Read Error Rate" },
            { 202, "Data Address Mark Errors" },
            { 231, "SSD Life Left" },
            { 232, "Available Reserved Space" },
            { 233, "Media Wearout Indicator" },
            { 234, "Thermal Throttle Status" },
            { 241, "Total LBAs Written" },
            { 242, "Total LBAs Read" }
        };

        private static string SmartNames(int id)
        {
            string name;
            return Names.TryGetValue(id, out name) ? name : "Attribute " + id;
        }
    }
}
