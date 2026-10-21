using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Berlit
{
    /// <summary>Один физический видеоадаптер, как его видит DXGI.</summary>
    internal sealed class GpuAdapterInfo
    {
        public long Luid;
        public string Name = string.Empty;
        public uint VendorId;
        public uint DeviceId;
        public long DedicatedVideoMemory;
        public long SharedSystemMemory;

        public string VendorName
        {
            get
            {
                switch (VendorId)
                {
                    case 0x8086: return "Intel";
                    case 0x10DE: return "NVIDIA";
                    case 0x1002:
                    case 0x1022: return "AMD";
                    case 0x1414: return "Microsoft";
                    case 0x13B5: return "ARM";
                    case 0x5143: return "Qualcomm";
                    case 0x15AD: return "VMware";
                    case 0x1AF4: return "Red Hat";
                    case 0x80EE: return "VirtualBox";
                    default: return string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Перечисление видеоадаптеров через DXGI. Нужен только для того, чтобы
    /// сопоставить счётчики "GPU Engine" (они адресуются по LUID) с именами карт.
    /// Работает без прав администратора; при любой ошибке возвращает пустой список.
    /// </summary>
    internal static class DxgiAdapters
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public Luid AdapterLuid;
            public uint Flags;
        }

        [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFactory1
        {
            [PreserveSig] int SetPrivateData(ref Guid name, uint size, IntPtr data);
            [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr data);
            [PreserveSig] int GetPrivateData(ref Guid name, out uint size, IntPtr data);
            [PreserveSig] int GetParent(ref Guid iid, out IntPtr parent);
            [PreserveSig] int EnumAdapters(uint index, out IntPtr adapter);
            [PreserveSig] int MakeWindowAssociation(IntPtr window, uint flags);
            [PreserveSig] int GetWindowAssociation(out IntPtr window);
            [PreserveSig] int CreateSwapChain(IntPtr device, IntPtr desc, out IntPtr swapChain);
            [PreserveSig] int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);
            [PreserveSig] int EnumAdapters1(uint index, out IntPtr adapter);
            [PreserveSig] int IsCurrent();
        }

        [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAdapter1
        {
            [PreserveSig] int SetPrivateData(ref Guid name, uint size, IntPtr data);
            [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr data);
            [PreserveSig] int GetPrivateData(ref Guid name, out uint size, IntPtr data);
            [PreserveSig] int GetParent(ref Guid iid, out IntPtr parent);
            [PreserveSig] int EnumOutputs(uint index, out IntPtr output);
            [PreserveSig] int GetDesc(out IntPtr desc);
            [PreserveSig] int CheckInterfaceSupport(ref Guid iid, out long umdVersion);
            [PreserveSig] int GetDesc1(out AdapterDesc1 desc);
        }

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);

        private const uint AdapterFlagSoftware = 0x2;

        public static IList<GpuAdapterInfo> Enumerate()
        {
            var result = new List<GpuAdapterInfo>();
            IntPtr factoryPtr = IntPtr.Zero;
            IFactory1 factory = null;
            try
            {
                Guid iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
                if (CreateDXGIFactory1(ref iid, out factoryPtr) != 0 || factoryPtr == IntPtr.Zero) return result;

                factory = (IFactory1)Marshal.GetObjectForIUnknown(factoryPtr);
                Marshal.Release(factoryPtr);
                factoryPtr = IntPtr.Zero;

                for (uint i = 0; i < 16; i++)
                {
                    IntPtr adapterPtr;
                    if (factory.EnumAdapters1(i, out adapterPtr) != 0 || adapterPtr == IntPtr.Zero) break;

                    IAdapter1 adapter = null;
                    try
                    {
                        adapter = (IAdapter1)Marshal.GetObjectForIUnknown(adapterPtr);
                        Marshal.Release(adapterPtr);
                        adapterPtr = IntPtr.Zero;

                        AdapterDesc1 desc;
                        if (adapter.GetDesc1(out desc) != 0) continue;
                        if ((desc.Flags & AdapterFlagSoftware) != 0) continue;   // программный рендер пропускаем

                        var info = new GpuAdapterInfo();
                        info.Name = (desc.Description ?? string.Empty).Trim();
                        if (info.Name.Length == 0) info.Name = "GPU";
                        info.VendorId = desc.VendorId;
                        info.DeviceId = desc.DeviceId;
                        info.DedicatedVideoMemory = (long)desc.DedicatedVideoMemory.ToUInt64();
                        info.SharedSystemMemory = (long)desc.SharedSystemMemory.ToUInt64();
                        info.Luid = ((long)(uint)desc.AdapterLuid.HighPart << 32) | desc.AdapterLuid.LowPart;
                        result.Add(info);
                    }
                    finally
                    {
                        if (adapterPtr != IntPtr.Zero) Marshal.Release(adapterPtr);
                        if (adapter != null) Marshal.ReleaseComObject(adapter);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (factoryPtr != IntPtr.Zero) Marshal.Release(factoryPtr);
                if (factory != null)
                {
                    try { Marshal.ReleaseComObject(factory); }
                    catch { }
                }
            }
            return result;
        }
    }
}
