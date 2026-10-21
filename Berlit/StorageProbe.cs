using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Berlit
{
    /// <summary>
    /// Накопители без WMI и без прав администратора: тома берутся из DriveInfo,
    /// модель/серийник/тип шины — IOCTL-запросами к \\.\PHYSICALDRIVEn.
    /// Если IOCTL недоступен, том показывается отдельной строкой без модели.
    /// </summary>
    internal sealed class StorageProbe
    {
        public sealed class VolumeInfo
        {
            public string Root = string.Empty;
            public string Label = string.Empty;
            public string Format = string.Empty;
            public long Total;
            public long Free;
            public int DeviceNumber = -1;
        }

        public sealed class DiskInfo
        {
            public int Number = -1;
            public bool IsPhysical;
            public string Model = string.Empty;
            public string Serial = string.Empty;
            public string Bus = string.Empty;
            public long Size;
            public readonly List<VolumeInfo> Volumes = new List<VolumeInfo>();
        }

        private sealed class DiskProps
        {
            public string Model = string.Empty;
            public string Serial = string.Empty;
            public string Bus = string.Empty;
            public long Size;
        }

        private readonly Dictionary<int, DiskProps> _cache = new Dictionary<int, DiskProps>();

        public IList<DiskInfo> Read()
        {
            List<VolumeInfo> volumes = ReadVolumes();

            var physical = new List<DiskInfo>();
            var byNumber = new Dictionary<int, DiskInfo>();

            foreach (VolumeInfo volume in volumes)
            {
                if (volume.DeviceNumber < 0) continue;

                DiskInfo disk;
                if (!byNumber.TryGetValue(volume.DeviceNumber, out disk))
                {
                    disk = new DiskInfo { Number = volume.DeviceNumber, IsPhysical = true };
                    DiskProps props = GetProps(volume.DeviceNumber);
                    disk.Model = props.Model;
                    disk.Serial = props.Serial;
                    disk.Bus = props.Bus;
                    disk.Size = props.Size;
                    byNumber[volume.DeviceNumber] = disk;
                    physical.Add(disk);
                }
                disk.Volumes.Add(volume);
            }

            physical.Sort(delegate (DiskInfo a, DiskInfo b) { return a.Number.CompareTo(b.Number); });

            // Тома, для которых не удалось определить физический диск (например, A:).
            var orphans = new List<DiskInfo>();
            foreach (VolumeInfo volume in volumes)
            {
                if (volume.DeviceNumber >= 0) continue;
                var disk = new DiskInfo
                {
                    Number = -1,
                    IsPhysical = false,
                    Model = "Volume " + volume.Root.TrimEnd('\\'),
                    Size = volume.Total
                };
                disk.Volumes.Add(volume);
                orphans.Add(disk);
            }

            var result = new List<DiskInfo>();
            result.AddRange(physical);
            result.AddRange(orphans);
            return result;
        }

        private static List<VolumeInfo> ReadVolumes()
        {
            var result = new List<VolumeInfo>();
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return result;
            }

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) continue;
                    if (!drive.IsReady) continue;

                    var volume = new VolumeInfo();
                    volume.Root = drive.Name;
                    volume.Format = drive.DriveFormat;
                    try { volume.Label = drive.VolumeLabel; }
                    catch { volume.Label = string.Empty; }
                    volume.Total = drive.TotalSize;
                    volume.Free = drive.AvailableFreeSpace;
                    volume.DeviceNumber = QueryDeviceNumber(drive.Name);
                    result.Add(volume);
                }
                catch
                {
                    // Том отвалился между вызовами (съёмный носитель) — пропускаем.
                }
            }
            return result;
        }

        private static int QueryDeviceNumber(string root)
        {
            string path = @"\\.\" + root.TrimEnd('\\');
            IntPtr handle = NativeMethods.CreateFile(path, 0,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
            if (NativeMethods.IsInvalidHandle(handle)) return -1;

            int size = Marshal.SizeOf(typeof(NativeMethods.StorageDeviceNumber));
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int returned;
                if (NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                        IntPtr.Zero, 0, buffer, size, out returned, IntPtr.Zero))
                {
                    var number = (NativeMethods.StorageDeviceNumber)Marshal.PtrToStructure(
                        buffer, typeof(NativeMethods.StorageDeviceNumber));
                    return (int)number.DeviceNumber;
                }
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                NativeMethods.CloseHandle(handle);
            }
            return -1;
        }

        private DiskProps GetProps(int number)
        {
            DiskProps props;
            if (_cache.TryGetValue(number, out props)) return props;

            props = new DiskProps();
            IntPtr handle = NativeMethods.CreateFile(@"\\.\PHYSICALDRIVE" + number, 0,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);

            if (!NativeMethods.IsInvalidHandle(handle))
            {
                try
                {
                    ReadDescriptor(handle, props);
                    props.Size = ReadLength(handle);
                }
                catch
                {
                }
                finally
                {
                    NativeMethods.CloseHandle(handle);
                }
            }

            _cache[number] = props;
            return props;
        }

        private static void ReadDescriptor(IntPtr handle, DiskProps props)
        {
            const int bufferSize = 4096;
            IntPtr input = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.StoragePropertyQuery)));
            IntPtr output = Marshal.AllocHGlobal(bufferSize);
            try
            {
                var query = new NativeMethods.StoragePropertyQuery
                {
                    PropertyId = 0,     // StorageDeviceProperty
                    QueryType = 0       // PropertyStandardQuery
                };
                Marshal.StructureToPtr(query, input, false);

                int returned;
                if (!NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                        input, Marshal.SizeOf(typeof(NativeMethods.StoragePropertyQuery)),
                        output, bufferSize, out returned, IntPtr.Zero))
                    return;

                if (returned < Marshal.SizeOf(typeof(NativeMethods.StorageDeviceDescriptorHeader))) return;

                var header = (NativeMethods.StorageDeviceDescriptorHeader)Marshal.PtrToStructure(
                    output, typeof(NativeMethods.StorageDeviceDescriptorHeader));

                string vendor = ReadString(output, returned, header.VendorIdOffset);
                string product = ReadString(output, returned, header.ProductIdOffset);
                props.Serial = ReadString(output, returned, header.SerialNumberOffset);
                props.Bus = NativeMethods.BusName(header.BusType);
                props.Model = Clean(vendor + " " + product);
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(input);
                Marshal.FreeHGlobal(output);
            }
        }

        /// <summary>
        /// Полный размер диска. IOCTL_DISK_GET_LENGTH_INFO требует доступа на чтение
        /// (то есть администратора), а DISK_GET_DRIVE_GEOMETRY_EX отдаёт размер
        /// и обычному пользователю: DISK_GEOMETRY (24 байта) + DiskSize (Int64).
        /// </summary>
        private static long ReadLength(IntPtr handle)
        {
            const int bufferSize = 256;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int returned;
                if (NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
                        IntPtr.Zero, 0, buffer, bufferSize, out returned, IntPtr.Zero) && returned >= 32)
                {
                    return Marshal.ReadInt64(buffer, 24);
                }
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return 0;
        }

        private static string ReadString(IntPtr buffer, int bufferLength, uint offset)
        {
            if (offset == 0 || offset >= bufferLength) return string.Empty;
            try
            {
                string value = Marshal.PtrToStringAnsi(new IntPtr(buffer.ToInt64() + offset));
                return value == null ? string.Empty : value.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }
}
