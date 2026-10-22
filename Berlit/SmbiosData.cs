using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Berlit
{
    /// <summary>Одна структура SMBIOS: тип, отформатированная область и строки.</summary>
    internal sealed class SmbiosStructure
    {
        public byte Type;
        public ushort Handle;
        public byte[] Data;
        public readonly List<string> Strings = new List<string>();

        public byte Byte(int offset)
        {
            return offset >= 0 && offset < Data.Length ? Data[offset] : (byte)0;
        }

        public ushort Word(int offset)
        {
            return offset >= 0 && offset + 1 < Data.Length
                       ? (ushort)(Data[offset] | (Data[offset + 1] << 8))
                       : (ushort)0;
        }

        public uint Dword(int offset)
        {
            return offset >= 0 && offset + 3 < Data.Length
                       ? (uint)(Data[offset] | (Data[offset + 1] << 8) | (Data[offset + 2] << 16) | (Data[offset + 3] << 24))
                       : 0u;
        }

        /// <summary>Строка по индексу SMBIOS (1-based; 0 — строки нет).</summary>
        public string Str(int index)
        {
            if (index <= 0 || index > Strings.Count) return string.Empty;
            return Strings[index - 1];
        }

        /// <summary>Строка, индекс которой лежит в структуре по указанному смещению.</summary>
        public string Text(int offset)
        {
            return Str(Byte(offset));
        }
    }

    /// <summary>
    /// Чтение сырой таблицы SMBIOS через GetSystemFirmwareTable('RSMB').
    /// Работает без прав администратора и без службы WMI — именно это нужно
    /// для статических сведений о железе.
    /// </summary>
    internal static class SmbiosReader
    {
        private const uint RsbmSignature = 0x52534D42; // 'RSMB'

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetSystemFirmwareTable(uint signature, uint tableId, IntPtr buffer, uint size);

        private static List<SmbiosStructure> _cache;

        public static IList<SmbiosStructure> Read()
        {
            if (_cache != null) return _cache;

            _cache = new List<SmbiosStructure>();
            try
            {
                uint size = GetSystemFirmwareTable(RsbmSignature, 0, IntPtr.Zero, 0);
                if (size == 0) return _cache;

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (GetSystemFirmwareTable(RsbmSignature, 0, buffer, size) != size) return _cache;

                    byte[] data = new byte[size];
                    Marshal.Copy(buffer, data, 0, (int)size);
                    Parse(data, _cache);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
            }
            return _cache;
        }

        public static SmbiosStructure First(byte type)
        {
            foreach (SmbiosStructure structure in Read())
                if (structure.Type == type) return structure;
            return null;
        }

        public static IEnumerable<SmbiosStructure> All(byte type)
        {
            foreach (SmbiosStructure structure in Read())
                if (structure.Type == type) yield return structure;
        }

        private static void Parse(byte[] data, List<SmbiosStructure> target)
        {
            // Заголовок 'RSMB' (4) + длина (1) + версия (2) + docrev (1) + reserved (1) = 8 байт.
            int offset = 8;
            while (offset + 4 <= data.Length)
            {
                byte type = data[offset];
                byte length = data[offset + 1];
                if (length < 4) break;

                var structure = new SmbiosStructure
                {
                    Type = type,
                    Handle = (ushort)(data[offset + 2] | (data[offset + 3] << 8)),
                    Data = new byte[length]
                };
                Array.Copy(data, offset, structure.Data, 0, Math.Min(length, data.Length - offset));

                int p = offset + length;
                while (p < data.Length)
                {
                    if (data[p] == 0)
                    {
                        // Конец списка строк — два нуля подряд.
                        if (p + 1 < data.Length && data[p + 1] == 0)
                        {
                            p += 2;
                            break;
                        }

                        p++; // пустая строка
                        continue;
                    }

                    int start = p;
                    while (p < data.Length && data[p] != 0) p++;
                    structure.Strings.Add(Encoding.ASCII.GetString(data, start, p - start).TrimEnd());
                    // Терминирующий ноль обработается на следующей итерации.
                }

                target.Add(structure);
                offset = p;

                if (type == 127) break;
            }
        }
    }
}
